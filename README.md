# Project
FolderSync
Here is my application for C# Dev in QA.<br>
This console program syncs 2 chosen folders and logs the events on both the console interface and the log.txt file.
Both the config.json and FolderSync.exe can be found in the net6.0 directory.

HOW TO USE<br>
1.Complete the config.json file to specify the following: <br>
  -source folder path;<br>
  -replica folder path;<br>
  -sync interval(in seconds);<br>
  -log file path;(if log.txt cannot be found, it will create one in the specified directory)<br>
  Example of a source path: "C:/Users/Documents/Source"<br>
  Example of log path: "D:/Users/Documents/Logs" or "D:/Users/Documents/Logs/log.txt"<br>
2.Run the FolderSync.exe file<br>
3.Enjoy
