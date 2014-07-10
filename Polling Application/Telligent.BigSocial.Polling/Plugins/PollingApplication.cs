using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telligent.Evolution.Extensibility.Version1;
using Telligent.DynamicConfiguration.Components;
using UIApi = Telligent.Evolution.Extensibility.UI.Version1;
using System.Xml;

namespace Telligent.BigSocial.Polling.Plugins
{
	public class PollingApplication: IPlugin, IConfigurablePlugin, IRequiredConfigurationPlugin, IInstallablePlugin, IPluginGroup
	{
		PollFactoryDefaultWidgetProvider _widgetProvider;

		#region IPlugin Members

		public string Name
		{
			get { return "Polling Application"; }
		}

		public string Description
		{
			get { return "Adds support for defining polls within groups."; }
		}

		public void Initialize()
		{
			_widgetProvider = PluginManager.Get<PollFactoryDefaultWidgetProvider>().FirstOrDefault();
		}

		#endregion

		#region IConfigurablePlugin Members

		public PropertyGroup[] ConfigurationOptions
		{
			get 
			{
				PropertyGroup group = new PropertyGroup("setup", "Setup", 1);
				group.Properties.Add(new Property("connectionString", "Database Connection String", PropertyType.String, 1, "") { DescriptionText = "The connection string used to access a SQL 2008 or newer database. The user identified should have db_owner permissions to the database." });
				return new PropertyGroup[] { group };
			}
		}

		public void Update(IPluginConfiguration configuration)
		{
			InternalApi.PollingDataService.ConnectionString = configuration.GetString("connectionString");
		}

		#endregion

		#region IRequiredConfigurationPlugin Members

		public bool IsConfigured
		{
			get 
			{
				return InternalApi.PollingDataService.IsConnectionStringValid();
			}
		}

		#endregion

		#region IInstallablePlugin Members

		public void Install(Version lastInstalledVersion)
		{
			if (lastInstalledVersion == null || lastInstalledVersion.Major == 0)
				InternalApi.PollingDataService.Install();

			if (lastInstalledVersion == null || lastInstalledVersion <= new Version(1, 1))
				InternalApi.PollingDataService.Install("update-1.1.sql");

			InternalApi.PollingDataService.Install("storedprocedures.sql");

			#region Install Widgets

			_widgetProvider = PluginManager.Get<PollFactoryDefaultWidgetProvider>().FirstOrDefault();
			UIApi.FactoryDefaultScriptedContentFragmentProviderFiles.DeleteAllFiles(_widgetProvider);

			var definitionFiles = new string[] { 
				"PollingBreadcrumbs-Widget.xml",
				"PollingCreateEditPoll-Widget.xml",
				"PollingLinks-Widget.xml",
				"PollingPoll-Widget.xml",
				"PollingPollList-Widget.xml",
				"PollingTopPollList-Widget.xml",
				"PollingTitle-Widget.xml",
				"PollingAddCommentForm-Widget.xml",
				"PollingCommentList-Widget.xml"
			};

			foreach (string definitionFile in definitionFiles)
			{
				using (var stream = InternalApi.EmbeddedResources.GetStream("Telligent.BigSocial.Polling.Resources.Widgets." + definitionFile))
				{
					UIApi.FactoryDefaultScriptedContentFragmentProviderFiles.AddUpdateDefinitionFile(_widgetProvider, definitionFile, stream);
				}
			}

			var supplementaryFiles = new Dictionary<Guid,string[]>();
			supplementaryFiles[new Guid("86f521cd27fb43919261b3383c2ccb15")] = new string[] {
				"PollingCreateEditPoll/ui.js"
			};
			supplementaryFiles[new Guid("5f93753d405f4eca8faef2f9ed07b946")] = new string[] {
				"PollingPollList/pagedContent.vm"
			};
			supplementaryFiles[new Guid("7eaaab7a5d0d41be919a4f9a719097ab")] = new string[] {
				"PollingTopPollList/pagedContent.vm"
			};
			supplementaryFiles[new Guid("bfdae73a86be4384abb9c25456b15a03")] = new string[] {
				"PollingAddCommentForm/ui.js"
			};
			supplementaryFiles[new Guid("59c79018954f49d581191adf7032d376")] = new string[] {
				"PollingCommentList/load.vm",
				"PollingCommentList/ui.js"
			};
			
			foreach (Guid instanceId in supplementaryFiles.Keys)
			{
				foreach (string relativePath in supplementaryFiles[instanceId])
				{
					using (var stream = InternalApi.EmbeddedResources.GetStream("Telligent.BigSocial.Polling.Resources.Widgets." + relativePath.Replace("/", ".")))
					{
						UIApi.FactoryDefaultScriptedContentFragmentProviderFiles.AddUpdateSupplementaryFile(_widgetProvider, instanceId, relativePath.Substring(relativePath.LastIndexOf("/") + 1), stream);
					}
				}
			}

			#endregion

			#region Install latest version of the poll page into all group themes (and revert any configured defaults or contextul versions of these pages)
			
			XmlDocument xml;
			foreach (var theme in UIApi.Themes.List(UIApi.ThemeTypes.Group))
			{
				var themeName = "Fiji";
				if (theme.Name == "424eb7d9138d417b994b64bff44bf274") // use the Enterprise version of the page for the Enterprise theme
					themeName = "Enterprise";

				if (theme.IsConfigurationBased)
				{
					xml = new XmlDocument();
					xml.LoadXml(InternalApi.EmbeddedResources.GetString("Telligent.BigSocial.Polling.Resources.Pages.createeditpoll-" + themeName + "-Groups-Page.xml"));
					UIApi.ThemePages.AddUpdateFactoryDefault(theme, xml.SelectSingleNode("theme/contentFragmentPages/contentFragmentPage"));

					xml = new XmlDocument();
					xml.LoadXml(InternalApi.EmbeddedResources.GetString("Telligent.BigSocial.Polling.Resources.Pages.poll-" + themeName + "-Groups-Page.xml"));
					UIApi.ThemePages.AddUpdateFactoryDefault(theme, xml.SelectSingleNode("theme/contentFragmentPages/contentFragmentPage"));

					xml = new XmlDocument();
					xml.LoadXml(InternalApi.EmbeddedResources.GetString("Telligent.BigSocial.Polling.Resources.Pages.polls-" + themeName + "-Groups-Page.xml"));
					UIApi.ThemePages.AddUpdateFactoryDefault(theme, xml.SelectSingleNode("theme/contentFragmentPages/contentFragmentPage"));

					UIApi.ThemePages.DeleteDefault(theme, "createeditpoll", true);
					UIApi.ThemePages.DeleteDefault(theme, "poll", true);
					UIApi.ThemePages.DeleteDefault(theme, "polls", true);
				}
				else
				{
					// non-configured-based themes don't support editing factory default pages.

					xml = new XmlDocument();
					xml.LoadXml(InternalApi.EmbeddedResources.GetString("Telligent.BigSocial.Polling.Resources.Pages.createeditpoll-" + themeName + "-Groups-Page.xml"));
					UIApi.ThemePages.AddUpdateDefault(theme, xml.SelectSingleNode("theme/contentFragmentPages/contentFragmentPage"));

					xml = new XmlDocument();
					xml.LoadXml(InternalApi.EmbeddedResources.GetString("Telligent.BigSocial.Polling.Resources.Pages.poll-" + themeName + "-Groups-Page.xml"));
					UIApi.ThemePages.AddUpdateDefault(theme, xml.SelectSingleNode("theme/contentFragmentPages/contentFragmentPage"));

					xml = new XmlDocument();
					xml.LoadXml(InternalApi.EmbeddedResources.GetString("Telligent.BigSocial.Polling.Resources.Pages.polls-" + themeName + "-Groups-Page.xml"));
					UIApi.ThemePages.AddUpdateDefault(theme, xml.SelectSingleNode("theme/contentFragmentPages/contentFragmentPage"));
				}				

				UIApi.ThemePages.Delete(theme, "createeditpoll", true);
				UIApi.ThemePages.Delete(theme, "poll", true);
				UIApi.ThemePages.Delete(theme, "polls", true);
			}

			#endregion

			#region Install CSS Files

			foreach (var theme in UIApi.Themes.List(UIApi.ThemeTypes.Site))
			{
				if (theme.IsConfigurationBased)
				{
					var themeName = "Fiji";
					if (theme.Name == "424eb7d9138d417b994b64bff44bf274") // use the Enterprise version of the page for the Enterprise theme
						themeName = "Enterprise";

					using (var stream = InternalApi.EmbeddedResources.GetStream("Telligent.BigSocial.Polling.Resources.Css.polling-" + themeName + ".css"))
					{
						UIApi.ThemeFiles.AddUpdateFactoryDefault(theme, UIApi.ThemeProperties.CssFiles, "polling.css", stream, (int) stream.Length);
						stream.Seek(0, System.IO.SeekOrigin.Begin);
						UIApi.ThemeFiles.AddUpdate(theme, UIApi.ThemeContexts.Site, UIApi.ThemeProperties.CssFiles, "polling.css", stream, (int) stream.Length);
					}
				}
			}

			#endregion
		}

		public void Uninstall()
		{
			InternalApi.PollingDataService.UnInstall();

			#region Delete custom pages used to support polls (from factory defaults, configured defaults, and contextual pages)
			
			foreach (var theme in UIApi.Themes.List(UIApi.ThemeTypes.Group))
			{
				if (theme.IsConfigurationBased)
				{
					UIApi.ThemePages.DeleteFactoryDefault(theme, "createeditpoll", true);
					UIApi.ThemePages.DeleteFactoryDefault(theme, "poll", true);
					UIApi.ThemePages.DeleteFactoryDefault(theme, "polls", true);
				}

				UIApi.ThemePages.DeleteDefault(theme, "createeditpoll", true);
				UIApi.ThemePages.DeleteDefault(theme, "poll", true);
				UIApi.ThemePages.DeleteDefault(theme, "polls", true);

				UIApi.ThemePages.Delete(theme, "createeditpoll", true);
				UIApi.ThemePages.Delete(theme, "poll", true);
				UIApi.ThemePages.Delete(theme, "polls", true);
			}

			#endregion

			#region Remove Widget Files

			UIApi.FactoryDefaultScriptedContentFragmentProviderFiles.DeleteAllFiles(_widgetProvider);

			#endregion

			#region Uninstall CSS Files

			foreach (var theme in UIApi.Themes.List(UIApi.ThemeTypes.Site))
			{
				if (theme.IsConfigurationBased)
				{
					UIApi.ThemeFiles.Remove(theme, UIApi.ThemeContexts.Site, UIApi.ThemeProperties.CssFiles, "polling.css");
					UIApi.ThemeFiles.RemoveFactoryDefault(theme, UIApi.ThemeProperties.CssFiles, "polling.css");
				}
			}

			#endregion
		}

		public Version Version
		{
			get { return GetType().Assembly.GetName().Version; }
		}

		#endregion

		#region IPluginGroup Members

		public IEnumerable<Type> Plugins
		{
			get 
			{
				return new Type[] { 
					typeof(PollContentType),
					typeof(PollSearchCategories),
					typeof(PollRestEndpoints),
					typeof(PollWidgetExtension),
					typeof(PollAnswerWidgetExtension),
					typeof(PollVoteWidgetExtension),
					typeof(PollUrlsWidgetExtension),
					typeof(PollWidgetContextProvider),
					typeof(PollFactoryDefaultWidgetProvider),
					typeof(PollGroupNavigation),
					typeof(PollViewer),
					typeof(PollHeaderExtension),
					typeof(PollNewPostLink),
					typeof(PollVotesMetric),
					typeof(TopPollsScore)
				};
			}
		}

		#endregion
	}
}
