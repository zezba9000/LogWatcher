using System.Windows;

namespace LogWatcher
{
	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow : Window
	{
		private XML.AppSettings settings;

		public SettingsWindow(XML.AppSettings settings)
		{
			InitializeComponent();
			this.settings = settings;

			emailToTextBox.Text = settings.emailTo;
			emailFromTextBox.Text = settings.emailFrom;
			emailSmtpHostTextBox.Text = settings.emailSmtpHost;
			emailSmtpPortTextBox.Text = settings.emailSmtpPort.ToString();

			if (Settings.GetWindowsEmailCredentials(out string username, out string password))
			{
				emailUsenameTextBox.Text = username;
				emailPasswordTextBox.Password = password;
			}
		}

		private void cancelButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void applyButton_Click(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrEmpty(emailToTextBox.Text) && !Settings.SetWindowsEmailCredentials(emailUsenameTextBox.Text, emailPasswordTextBox.Password))
			{
				MessageBox.Show(this, "Failed to save email username/password");
				return;
			}

			if (!int.TryParse(emailSmtpPortTextBox.Text, out settings.emailSmtpPort))
			{
				MessageBox.Show(this, "Invalid Email STMP port: " + emailSmtpPortTextBox.Text);
				return;
			}

			settings.emailTo = emailToTextBox.Text;
			settings.emailFrom = emailFromTextBox.Text;
			settings.emailSmtpHost = emailSmtpHostTextBox.Text;

			Close();
		}
	}
}
