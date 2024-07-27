namespace MobileSheetsExtractor;

using System.Security.Cryptography;

internal sealed class Song
{
	#region Private Data Members

	private static readonly char[] Separators = ['-', '–']; // Hyphen or en-dash

	#endregion

	#region Constructors

	public Song(FileInfo file, string basePath)
	{
		this.File = file;

		string baseName = Path.GetFileNameWithoutExtension(file.Name);
		this.Title = baseName;
		int separatorIndex = baseName.IndexOfAny(Separators);
		if (separatorIndex >= 0)
		{
			this.Title = baseName[..separatorIndex].Trim();
			this.Artists.Add(baseName[(separatorIndex + 1)..].Trim());
		}

		this.RelativeDirectory = file.Directory is null
			? string.Empty
			: file.Directory.FullName[basePath.Length..].TrimStart('\\').Replace('\\', '/');

		using FileStream fileStream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
		this.Hash = Convert.ToHexString(SHA256.HashData(fileStream));
	}

	#endregion

	#region Public Properties

	public FileInfo File { get; }

	public FileState FileState { get; set; }

	public string Title { get; set; }

	public List<string> Artists { get; } = [];

	public string RelativeDirectory { get; }

	public string Hash { get; }

	#endregion

	#region Public Methods

	public void Extract(string outputFolder)
	{
		string subfolder = this.FileState == FileState.Obsolete ? nameof(FileState.Obsolete) : "Files";
		string targetFolder = Path.Combine(outputFolder, subfolder);
		Directory.CreateDirectory(targetFolder);
		string targetFile = Path.Combine(targetFolder, this.File.Name);
		this.File.CopyTo(targetFile, true);
	}

	#endregion
}
