namespace AST.AzureBlobStorage.Model
{
    using Umbraco.Core.Models;
    using Umbraco.Web.Models;

    public class AzurePublishedContent : DynamicPublishedContent
    {
        public AzurePublishedContent(IPublishedContent content)
            : base(content)
        {
            this.Url = this.Url;
        }

        public new string Url { get; set; }
    }
}
