namespace AST.AzureBlobStorage.Helper
{
    using AST.AzureBlobStorage.Model;

    using Umbraco.Web;

    public static class MediaHelper
    {
        public static AzurePublishedContent ParseMedia(string mediaId, UmbracoHelper umbracoHelper)
        {
            var media = umbracoHelper.TypedMedia(mediaId);
            if (media != null)
            {
                var output = new AzurePublishedContent(umbracoHelper.TypedMedia(mediaId));
                output.Url = string.Format("{0}{1}", GlobalHelper.GetCdnDomain(), output.Url());
                return output;
            }

            return null;
        }
    }
}
