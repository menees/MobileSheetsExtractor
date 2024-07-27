namespace MobileSheetsExtractor;

using System.Text;

internal sealed class Args
{
	#region Constructors

	public Args(string[] args)
	{
		string GetArg(int index, string defaultValue) => index >= 0 && index < args.Length ? args[index].Trim() : defaultValue;

		this.OutputFolder = GetArg(0, string.Empty);
		this.InputFolder = GetArg(1, @"C:\Users\Bill\AppData\Local\Packages\41730Zubersoft.MobileSheets_ys1c8ct2g6ypr\LocalState");
		this.FileMasks = GetArg(2, "*.cho;*.pdf").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
#pragma warning disable MEN010 // Avoid magic numbers. Index is clear in context.
		this.DateTimePrefix = GetArg(3, "'");
#pragma warning restore MEN010 // Avoid magic numbers

		StringBuilder sb = new();
		if (this.OutputFolder.IsEmpty()
			|| this.OutputFolder.Equals("/?")
			|| this.OutputFolder.Equals("/help", StringComparison.OrdinalIgnoreCase))
		{
			sb.AppendLine("Usage: OutputFolder [InputFolder] [FileMasks = *.cho;*.pdf] [DateTimePrefix = ']");
		}

		if (this.OutputFolder.IsNotEmpty() && !Directory.Exists(this.OutputFolder))
		{
			sb.AppendLine("The output folder does not exist.");
		}

		if (this.InputFolder.IsEmpty())
		{
			sb.AppendLine("The input folder cannot be empty.");
		}
		else if (!Directory.Exists(this.InputFolder))
		{
			sb.AppendLine("The input folder does not exist.");
		}

		if (this.FileMasks.Count == 0)
		{
			sb.AppendLine("You must specify a file mask (e.g., *.cho).");
		}

		this.HelpText = sb.ToString();
	}

	#endregion

	#region Public Properties

	public string OutputFolder { get; }

	public string InputFolder { get; }

	public IReadOnlyList<string> FileMasks { get; }

	public string DateTimePrefix { get; }

	public string HelpText { get; }

	#endregion
}
