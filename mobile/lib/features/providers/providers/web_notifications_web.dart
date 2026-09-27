// ignore_for_file: deprecated_member_use, avoid_web_libraries_in_flutter
import 'dart:html' as html;

void requestNotificationPermissions() {
  try {
    if (html.Notification.supported) {
      if (html.Notification.permission == 'default') {
        html.Notification.requestPermission();
      }
    }
  } catch (e) {
    // Ignore on unsupported web contexts
  }
}

void showWebNotification(String title, String body) {
  try {
    if (html.Notification.supported) {
      if (html.Notification.permission == 'granted') {
        html.Notification(title, body: body);
      } else if (html.Notification.permission == 'default') {
        html.Notification.requestPermission().then((perm) {
          if (perm == 'granted') {
            html.Notification(title, body: body);
          }
        });
      }
    }
  } catch (e) {
    // Ignore
  }
}

String getNotificationPermissionStatus() {
  try {
    if (html.Notification.supported) {
      return html.Notification.permission ?? 'unknown';
    }
    return 'unsupported';
  } catch (e) {
    return 'unsupported';
  }
}

void sendTestNotification() {
  try {
    if (html.Notification.supported) {
      if (html.Notification.permission == 'granted') {
        html.Notification('AssistLK Test Notification', body: 'Chrome desktop notifications are working properly!');
      } else {
        html.Notification.requestPermission().then((perm) {
          if (perm == 'granted') {
            html.Notification('AssistLK Test Notification', body: 'Chrome desktop notifications are working properly!');
          }
        });
      }
    }
  } catch (e) {
    // Ignore
  }
}
