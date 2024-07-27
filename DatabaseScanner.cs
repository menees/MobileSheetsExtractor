namespace MobileSheetsExtractor;

using System.Data;
using System.Data.SQLite;

internal sealed class DatabaseScanner
{
	#region Constructors

	public DatabaseScanner(string inputFolder, IEnumerable<Song> songs)
	{
		string databaseName = Path.Combine(inputFolder, "MobileSheets.db");
		string connectionString = $"Data Source={databaseName}";

		using SQLiteConnection connection = new();
		connection.ConnectionString = connectionString;
		connection.Open();

		Dictionary<int, Song> idToSongMap = GetSongDetails(connection, songs);
		GetSongArtists(connection, idToSongMap);
		GetSongKeys(connection, idToSongMap);
		GetSongTempos(connection, idToSongMap);

		this.SetLists = GetSetLists(connection, idToSongMap);
		this.Collections = GetCollections(connection, idToSongMap);
	}

	#endregion

	#region Public Properties

	public IReadOnlyDictionary<string, List<Song>> SetLists { get; }

	public IReadOnlyDictionary<string, List<Song>> Collections { get; }

	#endregion

	#region Private Methods

	private static Dictionary<int, Song> GetSongDetails(SQLiteConnection connection, IEnumerable<Song> songs)
	{
		using SQLiteCommand command = connection.CreateCommand();
		command.CommandText = """
			select
				s.Id as SongId,
				s.Title,
				s.Custom2 as Capo,
				f.Path as File,
				st.Type
			from Songs s
				join Files f on f.SongId = s.Id
				left join SourceTypeSongs sts on sts.SongId = s.Id
				left join SourceType st on st.Id = sts.SourceTypeId
			order by s.Title
			""";

		Dictionary<string, Song> relativeFileToSongMap = songs.ToDictionary(song => song.RelativeFileName, StringComparer.OrdinalIgnoreCase);
		using SQLiteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			int songId = reader.GetInt32(0);
			string title = reader.GetString(1);
			string? capoText = reader.IsDBNull(2) ? null : reader.GetString(2);
			string relativeFileName = reader.GetString(3);
			string? type = reader.IsDBNull(4) ? null : reader.GetString(4);

			if (relativeFileToSongMap.TryGetValue(relativeFileName, out Song? song))
			{
				song.Id = songId;
				song.Title = title;
				song.ContentType = type;
				if (byte.TryParse(capoText, out byte capo))
				{
					song.Capo = capo;
				}
			}
			else
			{
				Console.WriteLine($"Song {songId} \"{title}\" has no matching file at {relativeFileName}.");
			}
		}

		Dictionary<int, Song> result = songs.Where(song => song.Id is not null).ToDictionary(song => song.Id!.Value);
		return result;
	}

	private static void GetSongArtists(SQLiteConnection connection, Dictionary<int, Song> idToSongMap)
	{
		using SQLiteCommand command = connection.CreateCommand();
		command.CommandText = """
			select
				s.Id as SongId,
				ar.Name as Artist
			from Songs s
				left join ArtistsSongs ars on ars.SongId = s.Id
				left join Artists ar on ar.Id = ars.ArtistId
			where ar.Name is not null
			order by s.Title, ars.Id, ar.SortBy
			""";

		GetSongMultiInstanceField(command, idToSongMap, (reader, index) => reader.GetString(index), song => song.Artists);
	}

	private static void GetSongKeys(SQLiteConnection connection, Dictionary<int, Song> idToSongMap)
	{
		using SQLiteCommand command = connection.CreateCommand();
		command.CommandText = """
			select
				s.Id as SongId,
				k.Name as Key
			from Songs s
				left join KeySongs ks on ks.SongId = s.Id
				left join Key k on k.Id = ks.KeyId
			where k.Name is not NULL
			order by s.Title, ks.Id, k.SortBy
			""";

		GetSongMultiInstanceField(command, idToSongMap, (reader, index) => reader.GetString(index), song => song.Keys);
	}

	private static void GetSongTempos(SQLiteConnection connection, Dictionary<int, Song> idToSongMap)
	{
		using SQLiteCommand command = connection.CreateCommand();
		command.CommandText = """
			select
				s.Id as SongId,
				t.Tempo
			from Songs s
				left join Tempos t on t.SongId = s.Id
			where t.Tempo is not null
			order by s.Title, t.TempoIndex
			""";

		GetSongMultiInstanceField(command, idToSongMap, (reader, index) => reader.GetInt32(index), song => song.Tempos);
	}

	private static Dictionary<string, List<Song>> GetSetLists(SQLiteConnection connection, Dictionary<int, Song> idToSongMap)
	{
		using SQLiteCommand command = connection.CreateCommand();
		command.CommandText = """
			select
				sl.Name as Setlist,
				s.Id as SongId
				--,s.Title
			from Songs s
				left join SetlistSong sls on sls.SongId = s.Id
				left join Setlists sl on sl.Id = sls.SetlistId
			where sl.Name is not null
			order by sl.Name, sls.Id, sl.SortBy
			""";

		Dictionary<string, List<Song>> result = GetSongLists(command, idToSongMap);
		return result;
	}

	private static Dictionary<string, List<Song>> GetCollections(SQLiteConnection connection, Dictionary<int, Song> idToSongMap)
	{
		using SQLiteCommand command = connection.CreateCommand();
		command.CommandText = """
			select
				c.Name as Collection,
				s.Id as SongId
				--,s.Title
			from Songs s
				left join CollectionSong cs on cs.SongId = s.Id
				left join Collections c on c.Id = cs.CollectionId
			where c.Name is not null
			order by c.Name, cs.Id, c.SortBy
			""";

		Dictionary<string, List<Song>> result = GetSongLists(command, idToSongMap);
		return result;
	}

	private static void GetSongMultiInstanceField<T>(
		SQLiteCommand command,
		Dictionary<int, Song> idToSongMap,
		Func<IDataRecord, int, T> getValue,
		Func<Song, IList<T>> getCollection)
	{
		using SQLiteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			int songId = reader.GetInt32(0);
			T value = getValue(reader, 1);

			Song song = idToSongMap[songId];
			IList<T> collection = getCollection(song);
			if (!collection.Contains(value))
			{
				collection.Add(value);
			}
		}
	}

	private static Dictionary<string, List<Song>> GetSongLists(SQLiteCommand command, Dictionary<int, Song> idToSongMap)
	{
		Dictionary<string, List<Song>> result = new(StringComparer.OrdinalIgnoreCase);

		using SQLiteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			string setList = reader.GetString(0);
			int songId = reader.GetInt32(1);

			if (!result.TryGetValue(setList, out List<Song>? list))
			{
				list = [];
				result[setList] = list;
			}

			Song song = idToSongMap[songId];
			list.Add(song);
		}

		return result;

	}

	#endregion
}
