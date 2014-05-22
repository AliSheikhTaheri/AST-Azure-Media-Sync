namespace AST.AzureBlobStorage.FileSystem
{
    using System.IO;
    using System.Web;

    using Microsoft.Win32;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.StorageClient;

    public class AzureFileStorage
    {
        private readonly string stripFromPathForContainer;

        private readonly string stripFromPathForKey;

        private CloudBlobClient blobClient;

        public AzureFileStorage(
            string storageConnectionString, string stripFromPathForContainer = "", string stripFromPathForKey = "")
        {
            this.stripFromPathForContainer = stripFromPathForContainer;
            this.stripFromPathForKey = stripFromPathForKey;
            this.CreateBlobClient(storageConnectionString);
        }

        /// <summary>
        /// Helper method to create blob client
        /// </summary>
        /// <param name="storageConnectionString">Storage account connection string</param>
        private void CreateBlobClient(string storageConnectionString)
        {
            // Retrieve storage account from connection string
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            // Create the blob client
            this.blobClient = storageAccount.CreateCloudBlobClient();
        }

        /// <summary>
        /// Checks whether a file exists
        /// </summary>
        /// <param name="path">Network or web path to file's folder</param>
        /// <param name="fileName">File name</param>
        /// <returns>True if file exists, false if not</returns>
        public bool DoesFileExist(string path, string fileName)
        {
            // Retrieve reference to container
            var container = this.blobClient.GetContainerReference(this.GetContainer(path));

            // Retrieve reference to blob
            var blob = container.GetBlockBlobReference(this.GetKey(path, fileName));

            try
            {
                blob.FetchAttributes();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Saves an HTTP POSTed file
        /// </summary>
        /// <param name="stream">HTTP POSTed file</param>
        /// <param name="path">Network or web path to file's folder</param>
        /// <param name="fileName">File name</param>
        public void SavePostedFile(Stream stream, string path, string fileName)
        {
            var containerName = GetContainer(path);

            // Retrieve reference to container
            var container = this.blobClient.GetContainerReference(containerName);

            // Retrieve reference to a blob
            var blob = container.GetBlockBlobReference(this.GetKey(path.Replace(containerName, string.Empty), fileName));

            var regKey = Registry.ClassesRoot.OpenSubKey(Path.GetExtension(fileName).ToLower());
            if (regKey != null)
            {
                var contentType = regKey.GetValue("Content Type");

                if (contentType != null)
                {
                    blob.Properties.ContentType = contentType.ToString();
                }
            }

            // Create or overwrite the blob with contents posted file stream
            ////blob.Properties.ContentType = stream.ContentType;
            blob.UploadFromStream(stream);
        }

        /// <summary>
        /// Deletes a file
        /// </summary>
        /// <param name="path">Network or web path to file's folder</param>
        /// <param name="fileName">File name</param>
        public void DeleteFile(string path, string fileName)
        {
            var containerName = GetContainer(path);

            // Retrieve reference to a previously created container
            var container = blobClient.GetContainerReference(containerName);

            // Retrieve reference to blob
            var blob = container.GetBlockBlobReference(this.GetKey(path.Replace(containerName, string.Empty), fileName));

            // Delete the blob
            blob.Delete();
        }

        public void DeleteFolder(string path)
        {
            foreach (var blob in blobClient.ListBlobsWithPrefix(path))
            {
                ((CloudBlockBlob)blob).Delete();
            }
        }

        /// <summary>
        /// Downloads a file to a stream
        /// </summary>
        /// <param name="path">Network or web path to file's folder</param>
        /// <param name="fileName">File name</param>
        /// <returns>Stream result</returns>
        public Stream DownloadToStream(string path, string fileName)
        {
            // Retrieve reference to a previously created container
            var container = this.blobClient.GetContainerReference(this.GetContainer(path));

            // Retrieve reference to blob
            var blob = container.GetBlockBlobReference(this.GetKey(path, fileName));

            // Check if exists...
            try
            {
                blob.FetchAttributes();

                // Download to stream
                var memStream = new MemoryStream();
                blob.DownloadToStream(memStream);
                return memStream;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Helper method to get the container from the path
        /// </summary>
        /// <param name="path">Web path to file's folder</param>
        /// <returns>first folder</returns>
        private string GetContainer(string path)
        {
            // Remove parts of path not required
            if (!string.IsNullOrEmpty(this.stripFromPathForContainer))
            {
                path = path.Replace(this.stripFromPathForContainer, string.Empty);
            }

            // Return first folder left to get container
            return path.Contains("/") ? path.Split('/')[0] : path;
        }

        /// <summary>
        /// Helper to construct object key from path
        /// </summary>
        /// <param name="path">Web path to file's folder</param>
        /// <param name="fileName">File name</param>
        /// <returns>Key value</returns>
        private string GetKey(string path, string fileName)
        {
            // Remove parts of path not required
            if (!string.IsNullOrEmpty(this.stripFromPathForKey))
            {
                if (this.stripFromPathForKey.Contains("|"))
                {
                    foreach (var pathToStrip in this.stripFromPathForKey.Split('|'))
                    {
                        path = path.Replace(pathToStrip, string.Empty);
                    }
                }
                else
                {
                    path = path.Replace(this.stripFromPathForKey, string.Empty);
                }
            }

            // Ensure path is relative to root
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

            // Ensure path has trailing slash (if not empty)
            if (!string.IsNullOrEmpty(path) && !path.EndsWith("/"))
            {
                path = path + "/";
            }

            return Path.Combine(path, fileName);
        }

        /// <summary>
        /// Sets the content type for all files in the container 
        /// </summary>
        /// <param name="containerName">Name of container</param>
        /// <param name="contentType">Content type to set</param>
        public void SetContentTypeForContainer(string containerName, string contentType)
        {
            // Retrieve reference to a previously created container
            var container = this.blobClient.GetContainerReference(this.GetContainer(containerName));

            // Loop through files setting content type for each
            foreach (var blobItem in container.ListBlobs())
            {
                var blob = container.GetBlockBlobReference(blobItem.Uri.ToString());
                blob.FetchAttributes();
                blob.Properties.ContentType = contentType;
                blob.SetProperties();
            }
        }
    }
}
