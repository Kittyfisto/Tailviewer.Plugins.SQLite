using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Threading;
using log4net;
using Metrolib;
using Tailviewer.BusinessLogic;
using Tailviewer.BusinessLogic.LogFiles;

namespace SQLite
{
	public sealed class SQLiteLogFile
		: AbstractLogFile
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly string _fileName;
		private readonly object _syncRoot;
		private readonly List<LogLine> _lines;
		private int _lineCount;
		private int _maxCharactersPerLine;
		private bool _exists;
		private DateTime? _startTimestamp;
		private DateTime _lastModified;
		private Size _fileSize;

		public SQLiteLogFile(string fileName, ITaskScheduler scheduler) : base(scheduler)
		{
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));

			_fileName = fileName;
			_syncRoot = new object();
			_lines = new List<LogLine>();

			StartTask();
			// It is expected that the ctor execute pretty quick, so we defer won't actually open a connection to the database
			// until RunOnce...
		}

		protected override TimeSpan RunOnce(CancellationToken token)
		{
			try
			{
				var info = new FileInfo(_fileName);
				_fileSize = Size.FromBytes(info.Length);

				if (info.Exists)
				{
					var builder = new SQLiteConnectionStringBuilder
					{
						DataSource = _fileName,
						Version = 3
					};
					using (var connection = new SQLiteConnection(builder.ToString()))
					{
						connection.Open();
						int lineCount = GetNumberOfLogEntries(connection);
						if (lineCount < _lineCount)
						{
							// We'll assume that the table was cleared...
							Listeners.Flush();
							_lineCount = 0;
							lock (_syncRoot)
							{
								_lines.Clear();
							}

							var lines = ReadLogLines(connection, 0, lineCount);
							lock (_syncRoot)
							{
								_lines.AddRange(lines);
								_lineCount = lineCount;
							}

							Listeners.OnRead(_lineCount);
						}
						else if (lineCount > _lineCount)
						{
							var remaining = lineCount - _lineCount;
							var lines = ReadLogLines(connection, _lineCount, remaining);
							lock (_syncRoot)
							{
								_lines.AddRange(lines);
								_lineCount = lineCount;
							}

							Listeners.OnRead(_lineCount);
						}
						else
						{
							// We'll just assume that nothing relevant has been modified.
						}
					}

					_exists = true;
				}
				else
				{
					Clear();
					_exists = false;
				}
			}
			catch (IOException e)
			{
				Log.DebugFormat("Caught exception while trying to open the database: {0}", e);

				// It's unlikely, but possible that the file happened to be deleted in between File.Exists
				// and actually opening the connection. But we should actually behave as if we've taken the else
				// branch above...
				_exists = false;
			}
			catch (Exception e)
			{
				// It's considered bad form to let exception bubble through to the task scheduler, and therefore
				// we 'll log the exception (which hopefully doesn't happen every time) ourselves...
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
			}
			return TimeSpan.FromMilliseconds(500);
		}
		
		private IEnumerable<LogLine> ReadLogLines(SQLiteConnection connection, int startIndex, int count)
		{
			using (SQLiteCommand cmd = connection.CreateCommand())
			{
				cmd.CommandText = string.Format("SELECT Timestamp, Thread, Level, Logger, Message FROM LOG LIMIT {0}, {1}",
					startIndex, count);
				var lines = new List<LogLine>(count);
				using (SQLiteDataReader reader = cmd.ExecuteReader())
				{
					int index = startIndex;
					while (reader.Read())
					{
						var timestamp = GetTimestamp(reader.GetInt64(0));
						var thread = reader.GetString(1);
						var level = GetLevel(reader.GetString(2));
						string logger = reader.GetString(3);
						string message = reader.GetString(4);

						// Tailviewer does not (yet, as of 0.6) support a proper tabular display of log files
						// and therefore we have to format the entire thing 
						var logLine = string.Format("{0} [{1}] {2} {3} {4}", timestamp,
							thread,
							logger,
							level,
							message);
						lines.Add(new LogLine(index, index, logLine, level, timestamp));
						++index;
					}
				}
				return lines;
			}
		}

		private DateTime GetTimestamp(long ticksSinceEpoch)
		{
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return epoch + TimeSpan.FromTicks(ticksSinceEpoch);
		}

		private LevelFlags GetLevel(string level)
		{
			if (string.Equals(level, "DEBUG", StringComparison.InvariantCultureIgnoreCase))
				return LevelFlags.Debug;
			if (string.Equals(level, "INFO", StringComparison.InvariantCultureIgnoreCase))
				return LevelFlags.Info;
			if (string.Equals(level, "WARN", StringComparison.InvariantCultureIgnoreCase))
				return LevelFlags.Warning;
			if (string.Equals(level, "ERROR", StringComparison.InvariantCultureIgnoreCase))
				return LevelFlags.Error;
			if (string.Equals(level, "FATAL", StringComparison.InvariantCultureIgnoreCase))
				return LevelFlags.Fatal;

			return LevelFlags.None;
		}

		[Pure]
		private static int GetNumberOfLogEntries(SQLiteConnection connection)
		{
			using (SQLiteCommand cmd = connection.CreateCommand())
			{
				cmd.CommandText = "SELECT COUNT(*) FROM LOG";
				return Convert.ToInt32(cmd.ExecuteScalar());
			}
		}

		private void Clear()
		{
			Listeners.Reset();

			lock (_syncRoot)
			{
				_lines.Clear();
			}
		}

		public override void GetSection(LogFileSection section, LogLine[] dest)
		{
			// An ideal implementation would simply block and fetch the required data from the database
			// in order to reduce the memory footprint of the application (assuming that reading data is
			// pretty fast). However the current Tailviewer implementation calls GetSection from within
			// the UI thread when displaying the log file, which prohibits a blocking call that performs disk I/O.
			//
			// This ridiculous implementation will be fixed for the 1.0 release of tailviewer, upon which
			// this entire comment will disappear ;)

			lock (_syncRoot)
			{
				for (int i = 0; i < section.Count; ++i)
				{
					var index = section.Index + i;
					dest[i] = _lines[(int) index];
				}
			}
		}

		public override LogLine GetLine(int index)
		{
			lock (_syncRoot)
			{
				return _lines[index];
			}
		}

		public override int Count => _lineCount;

		public override int MaxCharactersPerLine => _maxCharactersPerLine;

		public override Size Size => _fileSize;

		public override bool Exists => _exists;

		public override DateTime? StartTimestamp => _startTimestamp;

		public override DateTime LastModified => _lastModified;
	}
}