using NotificationsExtensions;
using NotificationsExtensions.Toasts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace NotificationSample {
  /// <summary>
  /// An empty page that can be used on its own or navigated to within a Frame.
  /// </summary>
  public sealed partial class MainPage : Page {
    public MainPage() {
      this.InitializeComponent();
    }

private void Button_Click(object sender, RoutedEventArgs e) {
  ToastContent content = new ToastContent() {
    Launch = "app-defined-string",
    Visual = new ToastVisual() {
      BindingGeneric = new ToastBindingGeneric() {
        Children = {
          new AdaptiveText() {
            Text = "Photo Share"
          },
          new AdaptiveText() {
            Text = "Andrew sent you a picture"
          },
          new AdaptiveText() {
            Text = "See it in full size!"
          },
          new AdaptiveImage() {
            Source = "https://unsplash.it/360/180?image=1043"
          }
        },
        AppLogoOverride = new ToastGenericAppLogo() {
          Source = "https://unsplash.it/64?image=883",
          HintCrop = ToastGenericAppLogoCrop.Circle
        }
      }
    }
  };
  // Display toast
  ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(content.GetXml()));
}

private void Reminder_Click(object sender, RoutedEventArgs e) {

  ToastContent content = new ToastContent() {
    Launch = "action=viewEvent&eventId=1983",
    Scenario = ToastScenario.Reminder,
    Visual = new ToastVisual() {
      BindingGeneric = new ToastBindingGeneric() {
        Children = {
          new AdaptiveText() {
            Text = "Very Important Meeting"
          },
          new AdaptiveText() {
            Text = "Conf Room 2001 / Building 135"
          },
          new AdaptiveText() {
            Text = "10:00 AM - 10:30 AM"
          }
        }
      }
    },
    Actions = new ToastActionsCustom() {
      Inputs = {
        new ToastSelectionBox("snoozeTime") {
          DefaultSelectionBoxItemId = "15",
          Items = {
            new ToastSelectionBoxItem("5", "5 minutes"),
            new ToastSelectionBoxItem("15", "15 minutes"),
            new ToastSelectionBoxItem("60", "1 hour"),
            new ToastSelectionBoxItem("240", "4 hours"),
            new ToastSelectionBoxItem("1440", "1 day")
          }
        }
      },
      Buttons = {
        new ToastButtonSnooze() {
          SelectionBoxId = "snoozeTime"
        },
        new ToastButtonDismiss()
      }
    }
  };
  // Display toast
  ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(content.GetXml()));
}


    private void Notify_Click(object sender, RoutedEventArgs e) {
      var template = @"
<toast launch='app-defined-string'>
  <visual>
    <binding template='ToastGeneric'>
      <text>Photo Share</text>
      <text>Andrew sent you a picture</text>
      <text>See it in full size!</text>
      <image src='https://unsplash.it/360/180?image=1043' />
      <image placement='appLogoOverride' src='https://unsplash.it/64?image=883' hint-crop='circle' />
    </binding>
  </visual>
</toast>
";

      XmlDocument doc = new XmlDocument();
      doc.LoadXml(template.Trim());
      // Display toast
      ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(doc));
    }
  }
}
