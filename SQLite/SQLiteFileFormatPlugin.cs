using System;
using System.Collections.Generic;
using System.Threading;
using Tailviewer.BusinessLogic.LogFiles;
using Tailviewer.BusinessLogic.Plugins;

namespace SQLite
{
	public sealed class SQLiteFileFormatPlugin
		: IFileFormatPlugin
	{
		public string Author => "Simon Mießler";

		public Uri Website => new Uri("https://github.com/Kittyfisto/Tailviewer.Plugins.SQLite");

		public ILogFile Open(string fileName, ITaskScheduler taskScheduler)
		{
			return new SQLiteLogFile(fileName, taskScheduler);
		}

		public IReadOnlyList<string> SupportedExtensions => new [] {".db" };
	}
}