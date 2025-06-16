namespace MobileSheetsExtractor;

using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Menees.Chords;
using Menees.Chords.Transformers;

internal sealed class Extractor
{
	#region Private Data Members

	private readonly Args args;

	#endregion

	#region Constructors

	public Extractor(Args args)
	{
		this.args = args;

		// Make sure we start from scratch every time.
		Directory.Delete(this.OutputFolder, true);
		Directory.CreateDirectory(this.OutputFolder);
	}

	#endregion

	#region Private Properties

	private string OutputFolder => this.args.OutputFolder;

	#endregion

	#region Public Methods

	public void ExtractSongs(IEnumerable<Song> songs)
	{
		foreach (Song song in songs)
		{
			string subfolder = song.FileState == FileState.Obsolete ? nameof(FileState.Obsolete) : "Files";
			string targetFile = this.GetOutputFileFullName(song, subfolder);
			EnsureUniqueFile(targetFile);
			song.File.CopyTo(targetFile);
		}

		this.ExtractCsv(songs);
		this.Augment(songs);
		this.GenerateCleanUpScript(songs);
	}

	public void ExtractLists(IReadOnlyDictionary<string, List<Song>> nameToSongListMap, string subfolder)
	{
		string targetFolder = Path.Combine(this.OutputFolder, subfolder);
		Directory.CreateDirectory(targetFolder);

		foreach (var pair in nameToSongListMap)
		{
			string targetFile = Path.Combine(targetFolder, $"{pair.Key}.txt");
			string content = string.Join(Environment.NewLine, pair.Value.Select(song => song.Title));
			EnsureUniqueFile(targetFile);
			File.WriteAllText(targetFile, content);
		}
	}

	#endregion

	#region Private Methods

	private static void EnsureUniqueFile(string targetFile)
	{
		if (File.Exists(targetFile))
		{
			throw new IOException($"{targetFile} already exists and would be overwritten.");
		}
	}

	private static int FindInsertIndex(List<string> lines, List<string> existingDirectives)
	{
		// Some software (e.g., SongBook) assumes that the {title} directive will be the first line
		// If we only need to add a {capo} directive or something, make sure it comes after other
		// existing directives in priority order. If a song contains {key} already, then we may need
		// to add {title} before it and {capo} after it.
		int maxDirectiveIndex = -1;
		foreach (string directive in existingDirectives)
		{
			for (int index = 0; index < lines.Count; index++)
			{
				if (lines[index].TrimStart().StartsWith($"{{{directive}:"))
				{
					maxDirectiveIndex = Math.Max(index, maxDirectiveIndex);
					break;
				}
			}
		}

		int result = maxDirectiveIndex + 1;
		return result;
	}

	private void ExtractCsv(IEnumerable<Song> songs)
	{
		DataTable table = new();
		DataColumnCollection columns = table.Columns;

		DataColumn fileName = columns.Add("FileName");
		DataColumn fileType = columns.Add("FileType");
		DataColumn size = columns.Add("Size", typeof(long));
		DataColumn modified = columns.Add("Modified");
		DataColumn fileState = columns.Add("FileState");
		DataColumn title = columns.Add("Title");
		DataColumn artists = columns.Add("Artists");
		DataColumn keys = columns.Add("Keys");
		DataColumn tempos = columns.Add("Tempos");
		DataColumn capo = columns.Add("Capo", typeof(byte));
		DataColumn contentType = columns.Add("ContentType");
		DataColumn id = columns.Add("Id", typeof(int));
		DataColumn relativePath = columns.Add("RelativePath");

		// Using ';' instead of Environment.NewLine makes the file more visually pleasing.
		// Also, we use ';' below in Augment(...) for OnSong compatibility.
		const char MultiInstanceSeparator = ';';
		foreach (Song song in songs.OrderBy(song => song.File.Name).ThenBy(song => song.FileState))
		{
			DataRow row = table.NewRow();
			row[fileName] = song.File.Name;
			row[fileType] = song.File.Extension.TrimStart('.');
			row[size] = song.File.Length;
			row[modified] = $"{this.args.DateTimePrefix}{song.File.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}Z";
			row[fileState] = song.FileState.ToString();
			row[title] = song.Title;
			row[artists] = string.Join(MultiInstanceSeparator, song.Artists);
			row[keys] = string.Join(MultiInstanceSeparator, song.Keys);
			row[tempos] = string.Join(MultiInstanceSeparator, song.Tempos);
			row[capo] = song.Capo ?? (object)DBNull.Value;
			row[contentType] = song.ContentType ?? (object)DBNull.Value;
			row[id] = song.Id ?? (object)DBNull.Value;
			row[relativePath] = Path.GetDirectoryName(song.RelativeFileName);
			table.Rows.Add(row);
		}

		string csvFile = Path.Combine(this.OutputFolder, "Songs.csv");
		EnsureUniqueFile(csvFile);
		using StreamWriter writer = new(csvFile, false, Encoding.UTF8);
		CsvUtility.WriteTable(writer, table);
	}

	private void Augment(IEnumerable<Song> songs)
	{
		List<Song> parsableSongs = [.. songs
			.Where(song => song.FileState != FileState.Obsolete)
			.Where(s => s.File.Extension.Equals(".cho", StringComparison.OrdinalIgnoreCase))];

		foreach (Song song in parsableSongs)
		{
			List<string> lines = [.. File.ReadLines(song.File.FullName)];
			Document document = Document.Parse(string.Join(Environment.NewLine, lines));
			IReadOnlyList<Entry> entries = DocumentTransformer.Flatten(document.Entries);
			ILookup<string, ChordProDirectiveLine> directiveLookup = entries
				.OfType<ChordProDirectiveLine>()
				.Where(directive => directive.Argument.IsNotWhiteSpace())
				.ToLookup(directive => directive.LongName, StringComparer.OrdinalIgnoreCase);

			List<string> existingDirectives = [];
			CheckDirective("title", [song.Title]);
			CheckDirective("artist", song.Artists);
			CheckDirective("key", song.Keys);
			CheckDirective("tempo", song.Tempos);
			CheckDirective("capo", [song.Capo]);

			if (existingDirectives.Count > 0)
			{
				string targetFile = this.GetOutputFileFullName(song, "Augmented");
				EnsureUniqueFile(targetFile);
				string text = string.Join(song.NewLine, lines);
				File.WriteAllText(targetFile, text, song.Encoding);
			}

			void CheckDirective<T>(string directiveName, IEnumerable<T> values)
			{
				if (directiveLookup.Contains(directiveName))
				{
					existingDirectives.Add(directiveName);
				}
				else if (values.Any(value => value != null && value.ToString().IsNotWhiteSpace()))
				{
					// ChordPro says, "Multiple artists can be specified using multiple directives."
					// https://www.chordpro.org/chordpro/directives-artist/
					// However, OnSong says, "You can specify multiple artists by separating names with a semi-colon."
					// OnSong 2024 doesn't support duplicate {artist: ...} directives, and it also requires a space
					// after the semicolon.
					// https://onsongapp.com/docs/features/formats/chordpro/
					const string OnSongSeparator = "; ";
					string argument = string.Join(OnSongSeparator, values);
					string directiveLine = $"{{{directiveName}: {argument}}}";

					int insertIndex = FindInsertIndex(lines, existingDirectives);
					lines.Insert(insertIndex, directiveLine);
					existingDirectives.Add(directiveName);
				}
			}
		}
	}

	private string GetOutputFileFullName(Song song, string subfolder)
	{
		// We might want to use the RelativeFileName instead of File.Name if we have duplicate file names
		// for different songs or different versions of the same song. But that leads to an ugly folder
		// structure. It's better to just make the input file names unique via MobileSheets first.
		string fileName = this.args.Flatten ? song.File.Name : song.RelativeFileName;
		string targetFile = Path.Combine(this.OutputFolder, subfolder, fileName);
		string? targetFolder = Path.GetDirectoryName(targetFile);
		if (targetFolder.IsNotEmpty())
		{
			Directory.CreateDirectory(targetFolder);
		}

		return targetFile;
	}

	private void GenerateCleanUpScript(IEnumerable<Song> songs)
	{
		List<Song> obsoleteSongs = [.. songs.Where(song => song.Id is null)];
		StringBuilder sb = new();
		foreach (Song song in obsoleteSongs)
		{
			sb.AppendLine($"Remove-Item -LiteralPath \"{song.File.FullName}\"");
			string? directory = song.File.DirectoryName;
			if (directory.IsNotEmpty() && !directory.Equals(this.args.InputFolder))
			{
				sb.AppendLine($"if (@(Get-ChildItem -LiteralPath \"{directory}\").Count -eq 0) {{ Remove-Item -LiteralPath \"{directory}\" }}");
			}
		}

		if (sb.Length > 0)
		{
			string targetFile = Path.Combine(this.OutputFolder, "CleanUpMobileSheetsInputFolder.ps1");
			EnsureUniqueFile(targetFile);
			string script = sb.ToString();
			File.WriteAllText(targetFile, script);
		}
	}

	#endregion
}
