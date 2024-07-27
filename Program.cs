namespace MobileSheetsExtractor;

using System.Data;
using static System.Console;

internal class Program
{
	static void Main(string[] rawArgs)
	{
		Args args = new(rawArgs);
		if (args.HelpText.IsNotEmpty())
		{
			WriteLine(args.HelpText);
		}
		else
		{
			FileScanner fileScanner = new(args);
			foreach (Song song in fileScanner.Songs)
			{
				song.Extract(args.OutputFolder);
			}

			// string csvFile = Path.Combine(args.OutputFolder, "Songs.csv");
			// FileUtility.TryDeleteFile(csvFile);
			// DataTable songData;
			// CsvUtility.WriteTable(csvFile, songData);
		}
	}
}
