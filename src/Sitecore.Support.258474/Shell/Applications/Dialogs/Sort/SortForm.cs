using System.Linq;

namespace Sitecore.Support.Shell.Applications.Dialogs.Sort
{
  using Sitecore;
  using Sitecore.Configuration;
  using Sitecore.Data;
  using Sitecore.Data.Comparers;
  using Sitecore.Data.Fields;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Globalization;
  using Sitecore.Pipelines;
  using Sitecore.Pipelines.ExpandInitialFieldValue;
  using Sitecore.Resources;
  using Sitecore.SecurityModel;
  using Sitecore.Shell.Applications.Dialogs.SortContent;
  using Sitecore.Text;
  using Sitecore.Web;
  using Sitecore.Web.UI;
  using Sitecore.Web.UI.HtmlControls;
  using Sitecore.Web.UI.Pages;
  using Sitecore.Web.UI.Sheer;
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Runtime.CompilerServices;
  using System.Web.UI;

  public class SortForm : DialogForm
  {
    private bool expandStandardValuesTokens;
    protected Scrollbox MainContainer;
    private string sortBy;

    private Item[] GetItemsToSort(Item item, string query)
    {
      Item[] itemArray;
      Assert.ArgumentNotNull(item, "item");
      Assert.IsNotNullOrEmpty(query, "query");
      try
      {
        using (new LanguageSwitcher(item.Language))
        {
          if (query.StartsWith("fast:"))
          {
            return (item.Database.SelectItems(query.Substring(5)) ?? new Item[0]);
          }
          return (item.Axes.SelectItems(query) ?? new Item[0]);
        }
      }
      catch (Exception exception)
      {
        itemArray = new Item[0];
        Log.Error("Failed to execute query:" + query, exception, this);
      }
      return itemArray;
    }

    private string GetSortBy(Item item)
    {
      Assert.ArgumentNotNull(item, "item");
      if (string.IsNullOrEmpty(this.sortBy))
      {
        return item.GetUIDisplayName();
      }
      Field sourceField = item.Fields[this.sortBy];
      if (sourceField == null)
      {
        return Translate.Text("[none]");
      }
      if (!this.expandStandardValuesTokens || !sourceField.ContainsStandardValue)
      {
        return StringUtil.RemoveTags(sourceField.Value);
      }
      ExpandInitialFieldValueArgs args = new ExpandInitialFieldValueArgs(sourceField, item);
      CorePipeline.Run("expandInitialFieldValue", args);
      return StringUtil.RemoveTags(args.Result ?? sourceField.Value);
    }

    private static bool IsEditable(Item item)
    {
      Assert.IsNotNull(item, "item");
      if ((!Context.IsAdministrator && item.Locking.IsLocked()) && !item.Locking.HasLock())
      {
        return false;
      }
      if (item.Appearance.ReadOnly)
      {
        return false;
      }
      return item.Access.CanWrite();
    }

    protected override void OnLoad(EventArgs e)
    {
      Assert.ArgumentNotNull(e, "e");
      base.OnLoad(e);
      if (!Context.ClientPage.IsEvent)
      {
        SortContentOptions options = SortContentOptions.Parse();
        this.sortBy = options.SortBy;
        this.expandStandardValuesTokens = options.ExpandStandardValuesTokens;
        string contentToSortQuery = options.ContentToSortQuery;
        Assert.IsNotNullOrEmpty(contentToSortQuery, "query");
        Item[] itemsToSort = this.GetItemsToSort(options.Item, contentToSortQuery);
        Array.Sort<Item>(itemsToSort, new DefaultComparer());
        if (itemsToSort.Length < 2)
        {
          base.OK.Disabled = true;
        }
        else
        {
          this.MainContainer.Controls.Clear();
          this.MainContainer.InnerHtml = this.Render(itemsToSort);
        }
      }
    }

    protected override void OnOK(object sender, EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");
      ListString str = new ListString(WebUtil.GetFormValue("sortorder"));
      if (str.Count == 0)
      {
        base.OnOK(sender, args);
      }
      else
      {
        this.Sort(from i in str select ShortID.DecodeID(i));
        SheerResponse.SetDialogValue("1");
        base.OnOK(sender, args);
      }
    }

    private string Render(IEnumerable<Item> items)
    {
      Assert.ArgumentNotNull(items, "items");
      HtmlTextWriter writer = new HtmlTextWriter(new StringWriter());
      writer.Write("<ul id='sort-list'>");
      foreach (Item item in items)
      {
        this.Render(writer, item);
      }
      writer.Write("</ul>");
      return writer.InnerWriter.ToString();
    }

    private void Render(HtmlTextWriter writer, Item item)
    {
      Assert.ArgumentNotNull(writer, "writer");
      Assert.ArgumentNotNull(item, "item");
      bool flag = IsEditable(item);
      string sortBy = this.GetSortBy(item);
      string str2 = !flag
        ? Translate.Text("You cannot edit this item because you do not have write access to it.")
        : sortBy;
      writer.Write("<li id='{0}' class='sort-item {1}' title='{2}'>", item.ID.ToShortID(),
        flag ? "editable" : "non-editable", str2);
      writer.Write("<img src='/sitecore/shell/Themes/Standard/Images/draghandle9x15.png' class='drag-handle' />");
      writer.Write("<img src='{0}' class='item-icon' />",
        Images.GetThemedImageSource(item.Appearance.Icon, ImageDimension.id16x16));
      writer.Write("<span unselectable='on' class='item-name'>{0}</span>", StringUtil.Clip(sortBy, 40, true));
      writer.Write("</li>");
    }

    private void Sort(IEnumerable<ID> orderList)
    {
      Assert.ArgumentNotNull(orderList, "orderList");
      SortContentOptions options = SortContentOptions.Parse();
      int defaultSortOrder = Settings.DefaultSortOrder;
      Item[] itemsToSort = this.GetItemsToSort(options.Item, options.ContentToSortQuery);
      foreach (ID id in orderList)
      {
        ID idToFind = id;
        Item item = Array.Find<Item>(itemsToSort, i => i.ID == idToFind);
        if (item != null)
        {
          using (new SecurityDisabler())
          {
            using (new EditContext(item, false, false))
            {
              item.Appearance.Sortorder = defaultSortOrder;
            }
          }
          defaultSortOrder += 100;
        }
      }
    }
  }
}
