namespace MobileSheetsExtractor;

internal enum FileState
{
	/// <summary>
	/// A distinct hash and file name
	/// </summary>
	Unique,

	/// <summary>
	/// The first occurrence of a duplicate hash or the latest version of a duplicate name
	/// </summary>
	Preferred,

	/// <summary>
	/// A subsequent occurrence of a duplicate hash or an older version of a duplicate name
	/// </summary>
	Obsolete,
}