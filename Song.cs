namespace MobileSheetsExtractor;

using System.Security.Cryptography;
using System.Text;

internal sealed class Song
{
	#region Private Data Members

	private static readonly char[] Separators = ['-', '–']; // Hyphen or en-dash
	private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

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

		// MobileSheets stores paths with '/' separators.
		this.RelativeFileName = file.FullName[basePath.Length..].TrimStart('\\').Replace('\\', '/');

		using FileStream fileStream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
		this.Hash = Convert.ToHexString(SHA256.HashData(fileStream));

		fileStream.Seek(0, SeekOrigin.Begin);
		using var reader = new StreamReader(fileStream, Utf8NoBom);
		string content = reader.ReadToEnd();
		this.Encoding = reader.CurrentEncoding;
		this.NewLine = content.Contains("\r\n") ? "\r\n" : "\n";
	}

	#endregion

	#region Public Properties

	public FileInfo File { get; }

	public Encoding Encoding { get; }

	public string NewLine { get; }

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
}
