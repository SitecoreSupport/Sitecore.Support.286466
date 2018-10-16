using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Links;
using Sitecore.StringExtensions;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Data;
using Sitecore.WFFM.Abstractions.Dependencies;
using Sitecore.WFFM.Abstractions.Mail;
using Sitecore.WFFM.Abstractions.Shared;
using Sitecore.WFFM.Abstractions.Utils;
using System;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Sitecore.Support.Forms.Core.Pipelines
{
  public class ProcessMessage : Sitecore.Forms.Core.Pipelines.ProcessMessage
  {
    public new void AddAttachments(ProcessMessageArgs args)
    {
      Assert.IsNotNull(ItemRepository, "ItemRepository");
      if (args.IncludeAttachment)
      {
        foreach (AdaptedControlResult field in args.Fields)
        {
          if (!string.IsNullOrEmpty(field.Parameters) && field.Parameters.StartsWith("medialink") && !string.IsNullOrEmpty(field.Value))
          {
            ItemUri itemUri = ItemUri.Parse(field.Value);
            if (itemUri != (ItemUri)null)
            {
              Item item = ItemRepository.GetItem(itemUri);
              if (item != null)
              {
                MediaItem mediaItem = new MediaItem(item);
                System.IO.Stream stream = mediaItem.GetMediaStream();
                if (stream == null)
                  stream = new System.IO.MemoryStream();
                args.Attachments.Add(new Attachment(stream, string.Join(".", mediaItem.Name, mediaItem.Extension), mediaItem.MimeType));
              }
            }
          }
        }
      }
    }
  }
}