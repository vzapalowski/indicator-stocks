using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Timers;
using System.Runtime.InteropServices;
using System.IO;
using Gtk;
using Gdk;
using Pango;
using AppIndicator;
using ce3a.Yahoo.Finance;

namespace indicatorstocks
{
	public class Indicator
	{
		private ApplicationIndicator indicator;
		private Menu menu;

		private Configuration config = Configuration.Instance;
		private System.Timers.Timer timer;

		private Screen screen = Screen.Default;
		private Pango.Layout layout;

		private readonly int spaceWidth;
		private static readonly string quoteUnknown = "???";
		private static readonly char symbolPadChar = '\x2007';
		private static readonly string symbolQuoteSeparator = "\t";

		private int maxWidth = 0;

		private string[] symbols;
		private string[] Symbols
		{
			get { return symbols; }
			set 
			{ 
				symbols = value;
				menu.Dispose();
				BuildMenu();
			}
		}

		public Indicator(string name)
		{
			indicator = new ApplicationIndicator(name, name, Category.ApplicationStatus);

			symbols = config.GetSymbols();

			layout = new Pango.Layout(PangoHelper.ContextGetForScreen(screen));
			layout.FontDescription = new FontDescription();

			spaceWidth = GetTextPixelLength(symbolPadChar.ToString());

			BuildMenu();
		}

		public void Start()
		{
			DoWork();

			timer = new System.Timers.Timer(config.UpdateInterval * 1000);
			timer.Elapsed += new ElapsedEventHandler(OnTimer);
			timer.Enabled = true;
			timer.AutoReset = true;

		}

		public static string GetSymbolFromMenuItem(MenuItem menuItem)
		{
			string symbol = ((Label)menuItem.Child).Text;
			return symbol.Substring(0, symbol.IndexOf(symbolPadChar));
		}

		private void BuildMenu()
		{
			indicator.Status = AppIndicator.Status.Passive;

			menu = new Menu();

			foreach (string symbol in symbols)
			{
				MenuItem menuItem = new MenuItem(symbol + ":" + quoteUnknown);
				menuItem.Activated += OnQuoteSelected;
				menu.Append(menuItem);

				maxWidth = Math.Max(maxWidth, GetTextPixelLength(symbol));
			}

			AddDefaultMenus(menu);
			menu.ShowAll();

			indicator.Menu   = menu;
			indicator.Status = AppIndicator.Status.Active;
		}

		private Menu AddDefaultMenus(Menu menu)
		{
			AccelGroup agr = new AccelGroup();

			menu.Append(new SeparatorMenuItem());

			ImageMenuItem menuItemPrefs = new ImageMenuItem(Stock.Preferences, agr);
			menuItemPrefs.Activated += OnPrefs;
			menuItemPrefs.AddAccelerator("activate", agr,
				new AccelKey(Gdk.Key.q, Gdk.ModifierType.ControlMask, AccelFlags.Visible));

			menu.Append(menuItemPrefs);

			ImageMenuItem menuItemHelp = new ImageMenuItem(Stock.Help, agr);
			menuItemHelp.Activated += OnHelp;
			menuItemHelp.AddAccelerator("activate", agr,
				new AccelKey(Gdk.Key.q, Gdk.ModifierType.ControlMask, AccelFlags.Visible));

			menu.Append(menuItemHelp);

			ImageMenuItem menuItemInfo = new ImageMenuItem(Stock.About, agr);
			menuItemInfo.Activated += OnInfo;
			menuItemInfo.AddAccelerator("activate", agr,
				new AccelKey(Gdk.Key.q, Gdk.ModifierType.ControlMask, AccelFlags.Visible));

			menu.Append(menuItemInfo);

			ImageMenuItem menuItemQuit = new ImageMenuItem(Stock.Quit, agr);
			menuItemQuit.Activated += OnQuit;
			menuItemQuit.AddAccelerator("activate", agr,
				new AccelKey(Gdk.Key.q, Gdk.ModifierType.ControlMask, AccelFlags.Visible));

			menu.Append(menuItemQuit);

			return menu;
		}

		private int GetTextPixelLength(string text)
		{
			int width, height;

		    layout.SetText(text);
		    layout.GetPixelSize(out width, out height);

			return width;
		}

		private void Update(float[] quotes)
		{
			Gtk.Application.Invoke(delegate {
				System.Collections.IEnumerator menuItemEnum = menu.AllChildren.GetEnumerator();
				System.Collections.IEnumerator symbolsEnum  = symbols.GetEnumerator();

				foreach (float quote in quotes)
				{
					if (menuItemEnum.MoveNext() && symbolsEnum.MoveNext())
					{
						// HACK:  symbol and quote aligmant based on string width in pixels.
						// FIXME: Consider using Pango.Layout instead.
						int curWidth = GetTextPixelLength(symbolsEnum.Current.ToString());
						int nbrOfPadChars = (maxWidth - curWidth) / spaceWidth + symbolsEnum.Current.ToString().Length + 1;

						Label label = (Label)((MenuItem)menuItemEnum.Current).Child;
						label.Text = 
							symbolsEnum.Current.ToString().PadRight(nbrOfPadChars, symbolPadChar) + 
								symbolQuoteSeparator + 
							(quote > 0 ? quote.ToString("0.00").PadLeft(8, symbolPadChar) : quoteUnknown);
					}
				}
		    });
		}

		private void DoWork()
		{
			float[] quotes = Quotes.GetQuotes(Symbols, Format.Bid);

			Update(quotes);
		}

		#region EVENT HANDLER
		protected void OnTimer(object sender, ElapsedEventArgs e)
		{
			DoWork();
		}

		protected void OnPrefs(object sender, EventArgs args)
		{
			PreferencesDialog preferencesDialog = new PreferencesDialog();

			preferencesDialog.Run();
			preferencesDialog.Destroy();

			// TODO:
			// cancel quotes request
			Start();

			Symbols = config.GetSymbols();
		}

		[DllImport ("glib-2.0.dll")]
		static extern IntPtr g_get_language_names ();

		protected void OnHelp(object sender, EventArgs args)
		{
			Assembly asm = Assembly.GetExecutingAssembly();

			foreach (var lang in GLib.Marshaller.NullTermPtrToStringArray (g_get_language_names (), false)) {
                string path = String.Format ("{0}/gnome/help/{1}/{2}",
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
				    (asm.GetCustomAttributes(
						typeof (AssemblyTitleAttribute), false)[0]
						as AssemblyTitleAttribute).Title,
				    lang);

                if (System.IO.Directory.Exists (path)) {
					Process.Start(String.Format("ghelp://{0}", path));
                    break;
                }
            }
		}

		protected void OnInfo(object sender, EventArgs args)
		{
			AboutDialog dialog = new AboutDialog();
			Assembly asm = Assembly.GetExecutingAssembly();
			
			dialog.ProgramName = (asm.GetCustomAttributes(
				typeof (AssemblyTitleAttribute), false)[0]
				as AssemblyTitleAttribute).Title;
			
			dialog.Version = String.Format("{0}.{1}.{2}",
			                               asm.GetName().Version.Major,
			                               asm.GetName().Version.Minor,
			                               asm.GetName().Version.Build);

			dialog.Comments = (asm.GetCustomAttributes(
				typeof (AssemblyDescriptionAttribute), false)[0]
				as AssemblyDescriptionAttribute).Description;

			dialog.Comments += String.Format("\n\nRevision: {0}", asm.GetName().Version.Revision);
			
			dialog.Copyright = (asm.GetCustomAttributes(
				typeof (AssemblyCopyrightAttribute), false)[0]
				as AssemblyCopyrightAttribute).Copyright;

			dialog.LogoIconName = About.LogoIconName;

			dialog.License = About.License;

			dialog.Authors = About.Authors;

			dialog.Artists = About.Artists;

			dialog.Response += delegate {
				dialog.Destroy();
			};
			
			dialog.Run();
		}

		protected void OnQuit(object sender, EventArgs args)
		{
			timer.Dispose();
			Application.Quit();
		}

		protected void OnQuoteSelected(object sender, EventArgs args)
		{
			string symbol = Indicator.GetSymbolFromMenuItem((MenuItem)sender);
			string url = Quotes.GetChartUrl(symbol);

			Process.Start(url);
		}
		#endregion
	}
}
