namespace AST.AzureBlobStorage.FileSystem
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using Umbraco.Core;
    using Umbraco.Core.IO;
    using Umbraco.Core.Logging;

    public class AzureFileSystem : IFileSystem
    {
        private readonly string _rootUrl;

        private AzureFileStorage azureFileStorage;

        #region constructors

        public AzureFileSystem(string virtualRoot, string azureConnectionString, string saveMediaToAzure)
        {
            if (virtualRoot == null)
            {
                throw new ArgumentNullException("virtualRoot");
            }

            if (!virtualRoot.StartsWith("~/"))
            {
                throw new ArgumentException("The virtualRoot argument must be a virtual path and start with '~/'");
            }

            this.RootPath = IOHelper.MapPath(virtualRoot);
            this._rootUrl = IOHelper.ResolveUrl(virtualRoot);

            this.AzureConnectionString = azureConnectionString;
            this.SaveMediaToAzure = bool.Parse(saveMediaToAzure);

            if (this.AzureIsValid)
            {
                this.azureFileStorage = new AzureFileStorage(azureConnectionString);
            }
        }

        public AzureFileSystem(string rootPath, string rootUrl)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                throw new ArgumentException("The argument 'rootPath' cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(rootUrl))
            {
                throw new ArgumentException("The argument 'rootUrl' cannot be null or empty.");
            }

            if (rootPath.StartsWith("~/"))
            {
                throw new ArgumentException("The rootPath argument cannot be a virtual path and cannot start with '~/'");
            }

            this.RootPath = rootPath;
            this._rootUrl = rootUrl;
        }

        #endregion

        internal string RootPath { get; private set; }

        private string AzureConnectionString { get; set; }


        private bool SaveMediaToAzure { get; set; }

        private bool AzureIsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(this.AzureConnectionString);
            }
        }

        #region Methods

        public void AddFile(string path, Stream stream)
        {
            this.AddFile(path, stream, true);
        }

        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            if (this.FileExists(path) && !overrideIfExists)
            {
                throw new InvalidOperationException(string.Format("A file at path '{0}' already exists", path));
            }

            this.EnsureDirectory(Path.GetDirectoryName(path));

            if (stream.CanSeek)
            {
                stream.Seek(0, 0);
            }

            using (var destination = (Stream)File.Create(this.GetFullPath(path)))
            {
                stream.CopyTo(destination);
            }

            if (this.AzureIsValid && this.SaveMediaToAzure)
            {
                if (stream.CanSeek)
                {
                    stream.Seek(0, 0);
                }

                this.azureFileStorage.SavePostedFile(stream, this.GetFolderStructureForAzure(path), this.GetFileNameFromPath(path));
            }
        }

        public void DeleteFile(string path)
        {
            if (!this.FileExists(path))
            {
                return;
            }

            try
            {
                File.Delete(this.GetFullPath(path));

                if (this.AzureIsValid && this.SaveMediaToAzure)
                {
                    this.azureFileStorage.DeleteFile(this.GetFolderStructureForAzure(path), this.GetFileNameFromPath(path));
                }
            }
            catch (FileNotFoundException ex)
            {
                LogHelper.Info<AzureFileSystem>(string.Format("DeleteFile failed with FileNotFoundException: {0}", ex.InnerException));
            }
        }

        public void DeleteDirectory(string path)
        {
            this.DeleteDirectory(path, false);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            if (!this.DirectoryExists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(this.GetFullPath(path), recursive);

                if (this.AzureIsValid && this.SaveMediaToAzure)
                {
                    azureFileStorage.DeleteFolder(GetFolderStructureForAzure(_rootUrl + path));
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                LogHelper.Error<AzureFileSystem>("Directory not found", ex);
            }
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            path = this.EnsureTrailingSeparator(this.GetFullPath(path));

            try
            {
                if (Directory.Exists(path))
                {
                    return Directory.EnumerateDirectories(path).Select(this.GetRelativePath);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogHelper.Error<AzureFileSystem>("Not authorized to get directories", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                LogHelper.Error<AzureFileSystem>("Directory not found", ex);
            }

            return Enumerable.Empty<string>();
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(this.GetFullPath(path));
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return this.GetFiles(path, "*.*");
        }

        public IEnumerable<string> GetFiles(string path, string filter)
        {
            path = this.EnsureTrailingSeparator(this.GetFullPath(path));

            try
            {
                if (Directory.Exists(path))
                {
                    return Directory.EnumerateFiles(path, filter).Select(this.GetRelativePath);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogHelper.Error<AzureFileSystem>("Not authorized to get directories", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                LogHelper.Error<AzureFileSystem>("Directory not found", ex);
            }

            return Enumerable.Empty<string>();
        }

        public Stream OpenFile(string path)
        {
            var fullPath = this.GetFullPath(path);
            return File.OpenRead(fullPath);
        }

        public bool FileExists(string path)
        {
            return File.Exists(this.GetFullPath(path));
        }

        public string GetRelativePath(string fullPathOrUrl)
        {
            var relativePath = fullPathOrUrl
                .TrimStart(this._rootUrl)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(this.RootPath)
                .TrimStart(Path.DirectorySeparatorChar);

            return relativePath;
        }

        public string GetFullPath(string path)
        {
            return !path.StartsWith(this.RootPath)
                ? Path.Combine(this.RootPath, path)
                : path;
        }

        public string GetUrl(string path)
        {
            return this._rootUrl.TrimEnd("/") + "/" + path
                .TrimStart(Path.DirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/')
                .TrimEnd("/");
        }

        public DateTimeOffset GetLastModified(string path)
        {
            return this.DirectoryExists(path)
                ? new DirectoryInfo(this.GetFullPath(path)).LastWriteTimeUtc
                : new FileInfo(this.GetFullPath(path)).LastWriteTimeUtc;
        }

        public DateTimeOffset GetCreated(string path)
        {
            return this.DirectoryExists(path)
                ? Directory.GetCreationTimeUtc(this.GetFullPath(path))
                : File.GetCreationTimeUtc(this.GetFullPath(path));
        }

        #endregion

        #region Helper Methods

        protected virtual void EnsureDirectory(string path)
        {
            path = this.GetFullPath(path);
            Directory.CreateDirectory(path);
        }

        protected string EnsureTrailingSeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                path = path + Path.DirectorySeparatorChar;
            }

            return path;
        }

        protected string GetFileNameFromPath(string path)
        {
            return Path.GetFileName(path);
        }

        protected string GetFolderStructureForAzure(string path)
        {
            var output = Path.Combine(this._rootUrl, path.Split('\\').Count() > 1 ? Path.GetDirectoryName(path) : path);

            if (output.StartsWith("/"))
            {
                output = output.Substring(1);
            }

            if (!output.EndsWith("/"))
            {
                output += "/";
            }

            return output;
        }

        #endregion
    }
}
