namespace MobileSheetsExtractor;

using System;
using System.Collections.Generic;

internal sealed class Extractor(string outputFolder)
{
	#region Public Methods

	public void ExtractSongs(IEnumerable<Song> songs)
	{
		foreach (Song song in songs)
		{
			string subfolder = song.FileState == FileState.Obsolete ? nameof(FileState.Obsolete) : "Files";
			string targetFolder = Path.Combine(outputFolder, subfolder);
			Directory.CreateDirectory(targetFolder);
			string targetFile = Path.Combine(targetFolder, song.File.Name);
			song.File.CopyTo(targetFile, true);
		}

		// TODO: Extract Songs.csv. [Bill, 7/27/2024]
		// TODO: Extract augmented .cho files with all info: Title, Artists, Keys, Tempos, Capo. [Bill, 7/27/2024]

		// string csvFile = Path.Combine(args.OutputFolder, "Songs.csv");
		// FileUtility.TryDeleteFile(csvFile);
		// DataTable songData;
		// CsvUtility.WriteTable(csvFile, songData);
	}

	public void ExtractLists(IReadOnlyDictionary<string, List<Song>> nameToSongListMap, string subfolder)
	{
		string targetFolder = Path.Combine(outputFolder, subfolder);
		Directory.CreateDirectory(targetFolder);

		foreach (var pair in nameToSongListMap)
		{
			string targetFile = Path.Combine(targetFolder, $"{pair.Key}.txt");
			string content = string.Join(Environment.NewLine, pair.Value.Select(song => song.Title));
			FileUtility.TryDeleteFile(targetFile);
			File.WriteAllText(targetFile, content);
		}
	}

	#endregion
}
