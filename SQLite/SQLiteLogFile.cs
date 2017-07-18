using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Threading;
using log4net;
using Metrolib;
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
			
		}

		public override LogLine GetLine(int index)
		{
			throw new NotImplementedException();
		}

		public override int Count => _lineCount;

		public override int MaxCharactersPerLine => _maxCharactersPerLine;

		public override Size Size => _fileSize;

		public override bool Exists => _exists;

		public override DateTime? StartTimestamp => _startTimestamp;

		public override DateTime LastModified => _lastModified;
	}
}