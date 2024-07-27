namespace MobileSheetsExtractor;

using System.Security.Cryptography;

internal sealed class Song
{
	#region Private Data Members

	private static readonly char[] Separators = ['-', '–']; // Hyphen or en-dash

	private readonly string baseName;

	#endregion

	#region Constructors

	public Song(FileInfo file, string basePath)
	{
		this.File = file;

		this.baseName = Path.GetFileNameWithoutExtension(this.File.Name);
		this.Title = this.baseName;

		// MobileSheets stores paths with '/' separators.
		this.RelativeFileName = file.FullName[basePath.Length..].TrimStart('\\').Replace('\\', '/');

		using FileStream fileStream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
		this.Hash = Convert.ToHexString(SHA256.HashData(fileStream));
	}

	#endregion

	#region Public Properties

	public FileInfo File { get; }

	public FileState FileState { get; set; }

	public string Title { get; set; }

	public List<string> Artists { get; } = [];

	/// <summary>
	/// The file name relative to <see cref="Args.InputFolder"/>.
	/// </summary>
	public string RelativeFileName { get; }

	public string Hash { get; }

	public int? Id { get; set; }

	public byte? Capo { get; set; }

	public string? ContentType { get; set; }

	public List<string> Keys { get; } = [];

	public List<int> Tempos { get; } = [];

	#endregion

	#region Public Methods

	public void InferArtist()
	{
		// We can't populate this.Artists in the constructor because it might infer a short
		// artist name from the file name, and the database may have a full artist name.
		// For example, we'd infer "Tom Petty" from "American Girl - Tom Petty.cho", but the
		// database lists the artist as "Tom Petty and the Heartbreakers".
		if (this.Artists.Count == 0)
		{
			int separatorIndex = this.baseName.IndexOfAny(Separators);
			if (separatorIndex >= 0)
			{
				this.Title = this.baseName[..separatorIndex].Trim();
				this.Artists.Add(this.baseName[(separatorIndex + 1)..].Trim());
			}
		}
	}

	#endregion
}
