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

namespace Sitecore.Forms.Core.Pipelines
{
  public class ProcessMessage
  {
    private readonly string srcReplacer;

    private readonly string shortHrefReplacer;

    private readonly string shortHrefMediaReplacer;

    private readonly string hrefReplacer;

    public IItemRepository ItemRepository
    {
      get;
      set;
    }

    public IFieldProvider FieldProvider
    {
      get;
      set;
    }

    public ProcessMessage()
        : this(DependenciesManager.WebUtil)
    {
    }

    public ProcessMessage(IWebUtil webUtil)
    {
      Assert.IsNotNull(webUtil, "webUtil");
      srcReplacer = string.Join(string.Empty, "src=\"", webUtil.GetServerUrl(), "/~");
      shortHrefReplacer = string.Join(string.Empty, "href=\"", webUtil.GetServerUrl(), "/");
      shortHrefMediaReplacer = string.Join(string.Empty, "href=\"", webUtil.GetServerUrl(), "/~/");
      hrefReplacer = shortHrefReplacer + "~";
    }

    public void ExpandLinks(ProcessMessageArgs args)
    {
      string value = LinkManager.ExpandDynamicLinks(args.Mail.ToString());
      args.Mail.Remove(0, args.Mail.Length);
      args.Mail.Append(value);
    }

    public void ExpandTokens(ProcessMessageArgs args)
    {
      Assert.IsNotNull(ItemRepository, "ItemRepository");
      Assert.IsNotNull(FieldProvider, "FieldProvider");
      foreach (AdaptedControlResult field in args.Fields)
      {
        IFieldItem fieldItem = ItemRepository.CreateFieldItem(ItemRepository.GetItem(field.FieldID));
        string value = field.Value;
        value = FieldProvider.GetAdaptedValue(field.FieldID, value);
        value = Regex.Replace(value, "src=\"/sitecore/shell/themes/standard/~", srcReplacer);
        value = Regex.Replace(value, "href=\"/sitecore/shell/themes/standard/~", hrefReplacer);
        value = Regex.Replace(value, "on\\w*=\".*?\"", string.Empty);
        if (args.MessageType == MessageType.Sms)
        {
          args.Mail.Replace(Sitecore.StringExtensions.StringExtensions.FormatWith("[{0}]", fieldItem.FieldDisplayName), value);
          args.Mail.Replace(Sitecore.StringExtensions.StringExtensions.FormatWith("[{0}]", fieldItem.Name), value);
        }
        else
        {
          if (!string.IsNullOrEmpty(field.Parameters) && args.IsBodyHtml)
          {
            if (field.Parameters.StartsWith("multipleline"))
            {
              value = value.Replace(Environment.NewLine, "<br/>");
            }
            if (field.Parameters.StartsWith("secure") && field.Parameters.Contains("<schidden>"))
            {
              value = Regex.Replace(value, "\\d", "*");
            }
          }
          string text = Regex.Replace(args.Mail.ToString(), "\\[<label id=\"" + fieldItem.ID + "\">[^<]+?</label>]", value);
          text = text.Replace(fieldItem.ID.ToString(), value);
          args.Mail.Clear().Append(text);
        }
        args.From = args.From.Replace("[" + fieldItem.ID + "]", value);
        args.From = args.From.Replace(fieldItem.ID.ToString(), value);
        args.To.Replace(string.Join(string.Empty, "[", fieldItem.ID.ToString(), "]"), value);
        args.To.Replace(string.Join(string.Empty, fieldItem.ID.ToString()), value);
        args.CC.Replace(string.Join(string.Empty, "[", fieldItem.ID.ToString(), "]"), value);
        args.CC.Replace(string.Join(string.Empty, fieldItem.ID.ToString()), value);
        args.Subject.Replace(string.Join(string.Empty, "[", fieldItem.ID.ToString(), "]"), value);
        args.From = args.From.Replace("[" + fieldItem.FieldDisplayName + "]", value);
        args.To.Replace(string.Join(string.Empty, "[", fieldItem.FieldDisplayName, "]"), value);
        args.CC.Replace(string.Join(string.Empty, "[", fieldItem.FieldDisplayName, "]"), value);
        args.Subject.Replace(string.Join(string.Empty, "[", fieldItem.FieldDisplayName, "]"), value);
        args.From = args.From.Replace("[" + field.FieldName + "]", value);
        args.To.Replace(string.Join(string.Empty, "[", field.FieldName, "]"), value);
        args.CC.Replace(string.Join(string.Empty, "[", field.FieldName, "]"), value);
        args.Subject.Replace(string.Join(string.Empty, "[", field.FieldName, "]"), value);
      }
    }

    public void AddHostToItemLink(ProcessMessageArgs args)
    {
      args.Mail.Replace("href=\"/", shortHrefReplacer);
    }

    public void AddHostToMediaItem(ProcessMessageArgs args)
    {
      args.Mail.Replace("href=\"~/", shortHrefMediaReplacer);
    }

    public void AddAttachments(ProcessMessageArgs args)
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
                args.Attachments.Add(new Attachment(mediaItem.GetMediaStream(), string.Join(".", mediaItem.Name, mediaItem.Extension), mediaItem.MimeType));
              }
            }
          }
        }
      }
    }

    public void BuildToFromRecipient(ProcessMessageArgs args)
    {
      if (!string.IsNullOrEmpty(args.Recipient) && !string.IsNullOrEmpty(args.RecipientGateway))
      {
        if (args.To.Length > 0)
        {
          args.To.Remove(0, args.To.Length);
        }
        args.To.Append(args.Fields.GetValueByFieldID(args.Recipient)).Append(args.RecipientGateway);
      }
    }

    public void SendEmail(ProcessMessageArgs args)
    {
      SmtpClient smtpClient = new SmtpClient(args.Host);
      smtpClient.EnableSsl = args.EnableSsl;
      SmtpClient smtpClient2 = smtpClient;
      if (args.Port != 0)
      {
        smtpClient2.Port = args.Port;
      }
      smtpClient2.Credentials = args.Credentials;
      smtpClient2.Send(GetMail(args));
    }

    private MailMessage GetMail(ProcessMessageArgs args)
    {
      MailMessage mail = new MailMessage(args.From.Replace(";", ","), args.To.Replace(";", ",").ToString(), args.Subject.ToString(), args.Mail.ToString())
      {
        IsBodyHtml = args.IsBodyHtml
      };
      if (args.CC.Length > 0)
      {
        mail.CC.Add(new MailAddress(args.CC.Replace(";", ",").ToString()));
      }
      if (args.BCC.Length > 0)
      {
        mail.Bcc.Add(new MailAddress(args.BCC.Replace(";", ",").ToString()));
      }
      args.Attachments.ForEach(delegate (Attachment attachment)
      {
        mail.Attachments.Add(attachment);
      });
      return mail;
    }
  }
}