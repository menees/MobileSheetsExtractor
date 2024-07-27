namespace MobileSheetsExtractor;

internal class Program
{
	static void Main(string[] rawArgs)
	{
		Args args = new(rawArgs);
		if (args.HelpText.IsNotEmpty())
		{
			Console.WriteLine(args.HelpText);
		}
		else
		{
			// First, read the raw files on disk and determine the preferred one to keep for duplicates.
			FileScanner fileScanner = new(args.InputFolder, args.FileMasks);

			// Next, read the SQLite database to get other song details.
			DatabaseScanner databaseScanner = new(args.InputFolder, fileScanner.Songs);

			Extractor extractor = new(args.OutputFolder, args.DateTimePrefix);

			// Use FileScanner's song list because there may be old files on disk that are no longer
			// in the database (e.g., PDFs that we're using CHO files for now instead).
			extractor.ExtractSongs(fileScanner.Songs);

			extractor.ExtractLists(databaseScanner.SetLists, nameof(databaseScanner.SetLists));
			extractor.ExtractLists(databaseScanner.Collections, nameof(databaseScanner.Collections));
		}
	}
}
