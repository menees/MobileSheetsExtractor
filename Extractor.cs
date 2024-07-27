namespace MobileSheetsExtractor;

using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Menees.Chords;
using Menees.Chords.Transformers;

internal sealed class Extractor(string outputFolder, string dateTimePrefix)
{
	#region Public Methods

	public void ExtractSongs(IEnumerable<Song> songs)
	{
		foreach (Song song in songs)
		{
			string subfolder = song.FileState == FileState.Obsolete ? nameof(FileState.Obsolete) : "Files";
			string targetFolder = EnsureFolder(outputFolder, subfolder);
			string targetFile = Path.Combine(targetFolder, song.File.Name);
			song.File.CopyTo(targetFile, true);
		}

		this.ExtractCsv(songs);
		this.Augment(songs);
	}

	public void ExtractLists(IReadOnlyDictionary<string, List<Song>> nameToSongListMap, string subfolder)
	{
		string targetFolder = EnsureFolder(outputFolder, subfolder);

		foreach (var pair in nameToSongListMap)
		{
			string targetFile = Path.Combine(targetFolder, $"{pair.Key}.txt");
			string content = string.Join(Environment.NewLine, pair.Value.Select(song => song.Title));
			FileUtility.TryDeleteFile(targetFile);
			File.WriteAllText(targetFile, content);
		}
	}

	#endregion

	#region Private Methods

	private static string EnsureFolder(string outputFolder, string subfolder)
	{
		string targetFolder = Path.Combine(outputFolder, subfolder);
		Directory.CreateDirectory(targetFolder);
		return targetFolder;
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
		const char MultiInstanceSeparator = ';';
		foreach (Song song in songs.OrderBy(song => song.Title))
		{
			DataRow row = table.NewRow();
			row[fileName] = song.File.Name;
			row[fileType] = song.File.Extension.TrimStart('.');
			row[size] = song.File.Length;
			row[modified] = $"{dateTimePrefix}{song.File.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}Z";
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

		string csvFile = Path.Combine(outputFolder, "Songs.csv");
		using StreamWriter writer = new(csvFile, false, Encoding.UTF8);
		CsvUtility.WriteTable(writer, table);
	}

	private void Augment(IEnumerable<Song> songs)
	{
		foreach (Song song in songs.Where(s => s.File.Extension.Equals(".cho", StringComparison.OrdinalIgnoreCase)))
		{
			Document document = Document.Load(song.File.FullName);
			IReadOnlyList<Entry> entries = DocumentTransformer.Flatten(document.Entries);
			ILookup<string, ChordProDirectiveLine> directiveLookup = entries
				.OfType<ChordProDirectiveLine>()
				.Where(directive => directive.Argument.IsNotWhiteSpace())
				.ToLookup(directive => directive.LongName, StringComparer.OrdinalIgnoreCase);

			List<string> augmentedLines = [];
			CheckDirective("title", [song.Title]);
			CheckDirective("artist", song.Artists);
			CheckDirective("key", song.Keys);
			CheckDirective("tempo", song.Tempos);
			CheckDirective("capo", [song.Capo]);

			if (augmentedLines.Count > 0)
			{
				augmentedLines.AddRange(File.ReadLines(song.File.FullName));
				string targetFolder = EnsureFolder(outputFolder, "Augmented");
				string targetFile = Path.Combine(targetFolder, song.File.Name);
				File.WriteAllLines(targetFile, augmentedLines);
			}

			void CheckDirective<T>(string directiveName, IEnumerable<T> values)
			{
				if (!directiveLookup.Contains(directiveName) && values.Any(value => value != null && value.ToString().IsNotWhiteSpace()))
				{
					// ChordPro says, "Multiple artists can be specified using multiple directives."
					// https://www.chordpro.org/chordpro/directives-artist/
					// However, OnSong says, "You can specify multiple artists by separating names with a semi-colon."
					// And OnSong 2024 doesn't support duplicate {artist: ...} directives. :-(
					// https://onsongapp.com/docs/features/formats/chordpro/
					const char OnSongSeparator = ';';
					string argument = string.Join(OnSongSeparator, values);
					augmentedLines.Add($"{{{directiveName}: {argument}}}");
				}
			}
		}
	}

	#endregion
}
