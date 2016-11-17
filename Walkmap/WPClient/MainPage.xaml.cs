using Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace WPClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        public string Site
        {
            get { return ConfigUtility.GetValue("Site"); }
        }

        public string Privacy
        {
            get { return ConfigUtility.GetValue("Privacy"); }
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if(e.NavigationMode == NavigationMode.New)
            {
                progressRing.IsActive = true;
                progressRing.Visibility = Visibility.Visible;

                Task.Run(new Action(CheckBinding));
            }
        }

        private async void CheckBinding()
        {
            try
            {
                KeyValuePair<string, string>[] postData = new KeyValuePair<string, string>[1];
                postData[0] = new KeyValuePair<string, string>("deviceUniqueId", UniqueIdUtility.GetUniqueId());
                string owner = await HttpUtility.GetHttpResponse(ConfigUtility.GetValue("GetDeviceOwnerAPI"), postData);

                if (owner == "")
                {
                    BackgroundTaskUtility.UnregisterBackgroundTask("MyTask");
                    ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
                    if (settings.Values.ContainsKey("Queue") == true)
                    {
                        settings.Values.Remove("Queue");
                    }
                }
                else
                {
                    BackgroundTaskUtility.RegisterBackgroundTask("RuntimeComponent.TrackingTask", "MyTask", new TimeTrigger(15, false), null);
                }

                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (owner == "")
                    {
                        ownerTextbox.Text = "";
                        ownerTextbox.IsEnabled = true;
                        deviceNameTextbox.Text = "";
                        deviceNameTextbox.IsEnabled = true;
                        bindButton.IsEnabled = true;
                        unbindButton.IsEnabled = false;
                    }
                    else
                    {
                        string[] ownerAndDeviceName = SerializerUtility.Deserialize<string[]>(owner);
                        ownerTextbox.Text = ownerAndDeviceName[0];
                        ownerTextbox.IsEnabled = false;
                        deviceNameTextbox.Text = ownerAndDeviceName[1];
                        deviceNameTextbox.IsEnabled = false;
                        bindButton.IsEnabled = false;
                        unbindButton.IsEnabled = true;
                    }
                    bindingGrid.Visibility = Visibility.Visible;
                });
            }
            catch(Exception ex)
            {
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    bindingGrid.Visibility = Visibility.Collapsed;
                    errorGrid.Visibility = Visibility.Visible;
                });
            }
            finally
            {
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    progressRing.IsActive = false;
                    progressRing.Visibility = Visibility.Collapsed;
                });
            }
        }

        private void unbindButton_Click(object sender, RoutedEventArgs e)
        {
            ownerTextbox.IsEnabled = false;
            deviceNameTextbox.IsEnabled = false;
            bindButton.IsEnabled = false;
            unbindButton.IsEnabled = false;
            progressRing.IsActive = true;
            progressRing.Visibility = Visibility.Visible;

            Task.Run(new Action(Unbinding));
        }

        private async void Unbinding()
        {
            try
            {
                KeyValuePair<string, string>[] postData = new KeyValuePair<string, string>[1];
                postData[0] = new KeyValuePair<string, string>("deviceUniqueId", UniqueIdUtility.GetUniqueId());
                await HttpUtility.GetHttpResponse(ConfigUtility.GetValue("UnbindDeviceAPI"), postData);
            }
            catch(Exception ex)
            {

            }

            CheckBinding();
        }

        private async void bindButton_Click(object sender, RoutedEventArgs e)
        {
            if(ownerTextbox.Text == "" || deviceNameTextbox.Text == "")
            {
                MessageDialog msg = new MessageDialog(StringResource.Instance["UserDeviceNameNotNull"]);
                msg.Title = StringResource.Instance["Error"];
                await msg.ShowAsync();
                return;
            }

            ownerTextbox.IsEnabled = false;
            deviceNameTextbox.IsEnabled = false;
            bindButton.IsEnabled = false;
            unbindButton.IsEnabled = false;
            progressRing.IsActive = true;
            progressRing.Visibility = Visibility.Visible;

            Task.Factory.StartNew(Binding, new string[2] { ownerTextbox.Text, deviceNameTextbox.Text });
        }

        private async void Binding(object state)
        {
            try
            {
                string[] ownerAndDeviceName = (string[])state;

                KeyValuePair<string, string>[] postData = new KeyValuePair<string, string>[3];
                postData[0] = new KeyValuePair<string, string>("deviceUniqueId", UniqueIdUtility.GetUniqueId());
                postData[1] = new KeyValuePair<string, string>("userId", ownerAndDeviceName[0]);
                postData[2] = new KeyValuePair<string, string>("deviceName", ownerAndDeviceName[1]);
                string returnValue = await HttpUtility.GetHttpResponse(ConfigUtility.GetValue("BindDeviceAPI"), postData);
                if(returnValue == "1")
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        MessageDialog msg = new MessageDialog(string.Format(StringResource.Instance["UserNameNotExist"], ownerAndDeviceName[0]));
                        msg.Title = StringResource.Instance["Error"];
                        await msg.ShowAsync();
                    });
                }
                if (returnValue == "2")
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        MessageDialog msg = new MessageDialog(string.Format(StringResource.Instance["DeviceNameExisted"], ownerAndDeviceName[1]));
                        msg.Title = StringResource.Instance["Error"];
                        await msg.ShowAsync();
                    });
                }
            }
            catch (Exception ex)
            {

            }

            CheckBinding();
        }
    }
}
