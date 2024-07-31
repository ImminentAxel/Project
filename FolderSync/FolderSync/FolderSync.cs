using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

// Class responsible for synchronizing the contents of two folders at regular intervals
class FolderSync
{
    // Fields to store folder paths, sync interval, log path, sync timer, and cancellation token source
    private readonly string sourcePath;
    private readonly string replicaPath;
    private readonly int intervalMs;
    private string logPath;
    private Timer syncTimer;
    private readonly CancellationTokenSource cts;

    // Constructor to initialize the fields with given parameters
    public FolderSync(string sourcePath, string replicaPath, int intervalSec, string logPath)
    {
        this.sourcePath = sourcePath;
        this.replicaPath = replicaPath;
        this.intervalMs = intervalSec * 1000; //seconds to milliseconds
        this.logPath = logPath;
        this.cts = new CancellationTokenSource();
    }

    private string LocateLogFile(string path)
    {

        // Check if the path is a directory or file
        if (Directory.Exists(path))
        {
            // If it's a directory, use log.txt within that directory
            path = Path.Combine(path, "log.txt");
        }
        else
        {
            // Ensure the directory exists if the path is a file path
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Console.WriteLine($"Creating directory: {directory}");
                Directory.CreateDirectory(directory);
            }
        }

        // Create the log file if it doesn't exist
        if (!File.Exists(path))
        {
            using (var stream = File.Create(path))
            {
                // File is created, stream will be disposed of immediately
            }
            Console.WriteLine($"Created log file: {path}");
        }
        else
        {
            Console.WriteLine($"Found existing log file: {path}");
        }

        return path;
    }

    // Method to start the synchronization process
    public void Start()
    {
        // Notify that synchronization has started
        Console.WriteLine("Synchronization started. Press [Enter] to exit and stop syncing...");

        // Locate or create the log file
        this.logPath = LocateLogFile(this.logPath);


        syncTimer = new Timer(Sync, null, 0, intervalMs); // Set up the timer to call Sync method at specified intervals
    }

    // Method to stop the synchronization process
    public void Stop()
    {
        cts.Cancel(); // Cancel the ongoing synchronization tasks
        syncTimer?.Dispose(); // Dispose of the timer
    }

    // Method that gets called at each timer interval to synchronize the directories
    private async void Sync(object state)
    {
        try
        {
            await SyncDirsAsync(new DirectoryInfo(sourcePath), new DirectoryInfo(replicaPath), cts.Token);
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}"); // Log any exceptions that occur during synchronization
        }
    }

    // Method to synchronize two directories asynchronously
    private async Task SyncDirsAsync(DirectoryInfo sourceDir, DirectoryInfo replicaDir, CancellationToken token)
    {
        if (!replicaDir.Exists)
        {
            replicaDir.Create(); // Create the replica directory if it doesn't exist
            Log($"Created directory: {replicaDir.FullName}");
        }

        // Get the files and subdirectories from source and replica directories
        var sourceFiles = sourceDir.GetFiles();
        var replicaFiles = replicaDir.GetFiles();
        var sourceSubDirs = sourceDir.GetDirectories();
        var replicaSubDirs = replicaDir.GetDirectories();

        // Copy files from source to replica asynchronously
        var copyTasks = sourceFiles.Select(file => CopyFileAsync(file, new FileInfo(Path.Combine(replicaDir.FullName, file.Name)), token)).ToList();
        await Task.WhenAll(copyTasks); // Wait for all copy tasks to complete

        // Recursively synchronize subdirectories
        var subDirTasks = sourceSubDirs.Select(dir => SyncDirsAsync(dir, new DirectoryInfo(Path.Combine(replicaDir.FullName, dir.Name)), token)).ToList();
        await Task.WhenAll(subDirTasks); // Wait for all subdirectory sync tasks to complete

        // Delete files in replica that don't exist in source
        foreach (var file in replicaFiles)
        {
            if (!sourceFiles.Any(f => f.Name == file.Name))
            {
                file.Delete();
                Log($"Deleted file: {file.FullName}");
            }
        }

        // Delete subdirectories in replica that don't exist in source
        foreach (var dir in replicaSubDirs)
        {
            if (!sourceSubDirs.Any(d => d.Name == dir.Name))
            {
                dir.Delete(true);
                Log($"Deleted directory: {dir.FullName}");
            }
        }
    }

    // Method to copy a file from source to replica asynchronously
    private async Task CopyFileAsync(FileInfo sourceFile, FileInfo replicaFile, CancellationToken token)
    {
        if (!replicaFile.Exists || !FilesAreIdentical(sourceFile.FullName, replicaFile.FullName))
        {
            await Task.Run(() =>
            {
                sourceFile.CopyTo(replicaFile.FullName, true); // Copy the file to the replica
                Log($"Copied file: {sourceFile.FullName} to {replicaFile.FullName}");
            }, token);
        }
    }

    // Method to compare if two files are identical by comparing their MD5 hashes
    private bool FilesAreIdentical(string filePath1, string filePath2)
    {
        using (var hashAlgorithm = MD5.Create())
        {
            using (var stream1 = File.OpenRead(filePath1))
            using (var stream2 = File.OpenRead(filePath2))
            {
                var hash1 = hashAlgorithm.ComputeHash(stream1);
                var hash2 = hashAlgorithm.ComputeHash(stream2);
                return hash1.SequenceEqual(hash2); // Compare the hashes of both files
            }
        }
    }

    // Method to log messages to console and log file
    private void Log(string message)
    {
        var logMessage = $"{DateTime.Now}: {message}";
        Console.WriteLine(logMessage);
        try
        {
            // Append the log message to the log file
            File.AppendAllText(logPath, logMessage + Environment.NewLine);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Unauthorized Access Error: {ex.Message}");
            // Handle access denied scenario, e.g., log to a different location or alert the user.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            // Handle other exceptions
        }
    }

    // Main method to read configuration, start synchronization, and handle user input
    static void Main(string[] args)
    {
        var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));

        if (config == null)
        {
            Console.WriteLine("Failed to load configuration.");
            return;
        }

        var sync = new FolderSync(config.SourcePath, config.ReplicaPath, config.IntervalSec, config.LogPath);
        sync.Start(); // Start synchronization

        Console.ReadLine(); // Wait for user to press Enter to stop the process

        sync.Stop();
        Console.WriteLine("Synchronization stopped.");
    }

    // Class to represent the configuration settings
    public class Config
    {
        public string SourcePath { get; set; }
        public string ReplicaPath { get; set; }
        public int IntervalSec { get; set; }
        public string LogPath { get; set; }
    }
}
