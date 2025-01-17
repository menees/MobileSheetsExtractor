﻿namespace MobileSheetsExtractor;

internal sealed class FileScanner(string inputFolder, IEnumerable<string> fileMasks)
{
	#region Public Properties

	public IReadOnlyList<Song> Songs { get; } = [.. fileMasks
			.SelectMany(mask => Directory.EnumerateFiles(inputFolder, mask, SearchOption.AllDirectories))
			.Select(file => new FileInfo(file))
			.Where(file => !file.Name.StartsWith("MobileSheets", StringComparison.OrdinalIgnoreCase)) // Ignore help pdfs
			.Select(file => new Song(file, inputFolder))];

	#endregion

	public void SetFileStates()
	{
		// If it has an ID from the database, then we know it's the most current song file
		// since MobileSheets only allows one file per song. Then we'll fallback to the most
		// recently modified file. If there's still a tie (old identical copies in multiple folders),
		// we'll take the one with the alphabetically earliest path.
		List<Song> orderedSongs = [.. this.Songs
			.OrderBy(song => song.Id ?? int.MaxValue)
			.ThenByDescending(song => song.File.LastWriteTimeUtc)
			.ThenBy(song => song.RelativeFileName)];

		// Mark preferred and obsolete duplicates based on hash and file name.
		SetGroupFileStates(orderedSongs.GroupBy(song => song.Hash));
		SetGroupFileStates(orderedSongs.GroupBy(song => song.File.Name));
	}

	#region Private Methods

	private static void SetGroupFileStates(IEnumerable<IGrouping<string, Song>> groups)
	{
		foreach (IGrouping<string, Song> group in groups)
		{
			if (group.Count() > 1)
			{
				bool first = true;
				foreach (Song song in group)
				{
					if (song.FileState == FileState.Unique)
					{
						song.FileState = first || song.Id != null ? FileState.Preferred : FileState.Obsolete;
					}

					first = false;
				}
			}
		}
	}

	#endregion
}
