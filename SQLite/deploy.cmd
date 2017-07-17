# This script is called while building and copies the entire plugin
# to a directory containing a tailviewer installation. Thus, debugging
# a plugin becomes incredibly easy when all you have to do is to press F5.

SET INSTALLATION_PATH=C:\Program Files\Tailviewer\Plugins\SQLite

copy SQLite.dll SQLite.tvp
mkdir "%INSTALLATION_PATH%"
copy SQLite.tvp "%INSTALLATION_PATH%\SQLite.tvp"
copy System.Data.SQLite.dll "%INSTALLATION_PATH%\System.Data.SQLite.dll"
mkdir "%INSTALLATION_PATH%\x86\"
copy x86\SQLite.Interop.dll "%INSTALLATION_PATH%\x86\"
mkdir "%INSTALLATION_PATH%\x64\"
copy x64\SQLite.Interop.dll "%INSTALLATION_PATH%\x64\"
