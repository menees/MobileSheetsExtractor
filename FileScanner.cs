﻿namespace MobileSheetsExtractor;

internal sealed class FileScanner
{
	#region Constructors

	public FileScanner(string inputFolder, IEnumerable<string> fileMasks)
	{
		this.Songs = [.. fileMasks
			.SelectMany(mask => Directory.EnumerateFiles(inputFolder, mask, SearchOption.AllDirectories))
			.Select(file => new Song(new FileInfo(file), inputFolder))
			.OrderByDescending(song => song.File.LastWriteTimeUtc)
			.ThenBy(song => song.RelativeFileName)];

		// Mark preferred and obsolete duplicates based on hash and file name.
		SetGroupFileStates(this.Songs.GroupBy(song => song.Hash));
		SetGroupFileStates(this.Songs.GroupBy(song => song.File.Name));
	}

	#endregion

	#region Public Properties

	public IReadOnlyList<Song> Songs { get; }

	#endregion

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
						song.FileState = first ? FileState.Preferred : FileState.Obsolete;
					}

					first = false;
				}
			}
		}
	}

	#endregion
}
