﻿/*
 * Licensed Materials - Property of IBM
 *
 * 5725E28, 5725I03
 *
 * © Copyright IBM Corp. 2016, 2016
 * US Government Users Restricted Rights - Use, duplication or disclosure restricted by GSA ADP Schedule Contract with IBM Corp.
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IBMMobilePush.Droid.API;
using IBMMobilePush.Droid.API.Broadcast;
using IBMMobilePush.Droid.API.Attribute;
using IBMMobilePush.Droid.Notification;
using IBMMobilePush.Droid.API.Notification;
using IBMMobilePush.Droid.API.Event;
using IBMMobilePush.Droid.Plugin.Inbox;
using IBMMobilePush.Droid.Location;
using IBMMobilePush.Droid.Beacons;
using Java.Util;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Bluetooth;
using Android.Support.V4.Content;
using Android.Content.PM;
using Newtonsoft.Json.Linq;
using Xamarin.Forms;
using SQLite.Net;
using SQLite.Net.Async;
using SQLite.Net.Platform.XamarinAndroid;
using Android.Locations;
using Android.Support.V4.App;
using IBMMobilePush.Droid.Plugin.InApp;

[assembly: Dependency(typeof(IBMMobilePush.Forms.Droid.IBMMobilePushImpl))]
namespace IBMMobilePush.Forms.Droid
{
    public static class Utilities
    {
        public static Java.Util.Date ConvertDate(DateTimeOffset timestamp, Java.Util.Date defaultValue)
        {
            if (timestamp != default(DateTimeOffset))
            {
                DateTime dt = DateTime.Now;
                long epocTicks = 621355968000000000L;
                return new Java.Util.Date((timestamp.UtcTicks - epocTicks) / TimeSpan.TicksPerMillisecond);
            }
            return defaultValue;
        }

        public static DateTimeOffset ConvertDate(Java.Util.Date timestamp, DateTimeOffset defaultValue)
        {
            if (timestamp != null && timestamp.Time > 0)
            {
                var referenceDate = (long)new DateTime(1970, 1, 1, 0, 0, 0).Subtract(DateTime.MinValue).TotalMilliseconds;
                return new DateTimeOffset((referenceDate + timestamp.Time) * TimeSpan.TicksPerMillisecond, TimeSpan.FromSeconds(0));
            }
            return defaultValue;
        }
    }

    public class IBMMobilePushImpl : IIBMMobilePush
    {
        public String XamarinPluginVersion()
        {
            return null;
        }

        public IBMMobilePushImpl()
        {
            ApplicationContext = Android.App.Application.Context;

            if (LocationAutoInitialize())
            {
                ManualLocationInitialization();
            }
            else
            {
                if (LocationInitialized())
                {
                    ManualLocationInitialization();
                }
            }
        }

        public GenericDelegate LocationAuthorizationChanged { set; get; }

        public bool LocationInitialized()
        {
            var prefs = ApplicationContext.GetSharedPreferences("MCE", FileCreationMode.Private);
            return prefs.GetBoolean("locationInitialized", false);
        }

        public void SetLocationInitialized(bool status)
        {
            var prefs = ApplicationContext.GetSharedPreferences("MCE", FileCreationMode.Private);
            var prefEditor = prefs.Edit();
            prefEditor.PutBoolean("locationInitialized", status);
            prefEditor.Commit();
        }

        public void ManualLocationInitialization()
        {
            if (ContextCompat.CheckSelfPermission(ApplicationContext, Android.Manifest.Permission.AccessFineLocation) == Permission.Granted)
            {
                IBMMobilePush.Droid.Location.LocationManager.EnableLocationSupport(ApplicationContext);
                SetLocationInitialized(true);
            }
            else
            {
                ActivityCompat.RequestPermissions((Activity)Android.App.Application.Context, new string[] { Android.Manifest.Permission.AccessFineLocation, Android.Manifest.Permission.Bluetooth, Android.Manifest.Permission.BluetoothAdmin }, 0);
            }

            if (LocationAuthorizationChanged != null)
                LocationAuthorizationChanged();
        }

        public void ManualSdkInitialization()
        {
            MceApplication.Reinitialize(ApplicationContext);
        }

        public bool LocationAutoInitialize()
        {
            try
            {
                var configHandle = ApplicationContext.Assets.Open("MceConfig.json");
                var configStream = new StreamReader(configHandle);
                string response = configStream.ReadToEnd();
                return JObject.Parse(response)["location"]["autoInitialize"].Value<bool>();
            }
            catch (Java.Lang.Exception ex)
            {
                return true;
            }
            catch (System.Exception ex)
            {
                return true;
            }
        }
        delegate void InboxCallbackDelegate();

        Context ApplicationContext;

        public RegistrationUpdatedDelegate RegistrationUpdated { set; get; }
        public AttributeResultsDelegate AttributeQueueResults { set; get; }
        public EventResultsDelegate EventQueueResults { set; get; }
        public GenericDelegate InboxMessagesUpdate { set; get; }
        public GenericDelegate LocationsUpdated { get; set; }
        public GeofenceDelegate GeofenceEntered { set; get; }
        public GeofenceDelegate GeofenceExited { set; get; }
        public BeaconDelegate BeaconEntered { set; get; }
        public BeaconDelegate BeaconExited { set; get; }


        public string Version()
        {
            return MceSdk.SdkVerNumber;
        }

        public string UserId()
        {
            var details = MceSdk.RegistrationClient.GetRegistrationDetails(ApplicationContext);
            return details.UserId;
        }

        public string ChannelId()
        {
            var details = MceSdk.RegistrationClient.GetRegistrationDetails(ApplicationContext);
            return details.ChannelId;
        }

        public string AppKey()
        {
            return MceSdk.RegistrationClient.GetAppKey(ApplicationContext);
        }

        IBMMobilePush.Droid.API.Attribute.Attribute AttributeConverter(string key, object value)
        {
            var stringValue = value as string;
            if (stringValue != null)
            {
                return new StringAttribute(key, stringValue);
            }

            var dateValue = value as Java.Util.Date;
            if (dateValue != null)
            {
                return new DateAttribute(key, dateValue);
            }

            try
            {
                var intValue = (int)Convert.ChangeType(value, typeof(int));
                return new NumberAttribute(key, new Java.Lang.Integer(intValue));
            }
            catch (InvalidCastException ex)
            {
            }


            try
            {
                var floatValue = (int)Convert.ChangeType(value, typeof(float));
                return new NumberAttribute(key, new Java.Lang.Float(floatValue));
            }
            catch (InvalidCastException ex)
            {
            }


            try
            {
                var doubleValue = (double)Convert.ChangeType(value, typeof(double));
                return new NumberAttribute(key, new Java.Lang.Double(doubleValue));
            }
            catch (InvalidCastException ex)
            {
            }


            try
            {
                var longValue = (long)Convert.ChangeType(value, typeof(long));
                return new NumberAttribute(key, new Java.Lang.Long(longValue));
            }
            catch (InvalidCastException ex)
            {
            }

            try
            {
                var boolValue = (bool)Convert.ChangeType(value, typeof(bool));
                return new BooleanAttribute(key, new Java.Lang.Boolean(boolValue));
            }
            catch (InvalidCastException ex)
            {
            }

            return null;
        }

        class AttributeCallback : Java.Lang.Object, IOperationCallback
        {
            IBMMobilePush.Droid.API.Attribute.Attribute attribute;
            AttributeResultsDelegate callback;
            AttributeOperation operation;
            string key;

            public AttributeCallback(string key, AttributeOperation operation, AttributeResultsDelegate callback)
            {
                this.key = key;
                this.callback = callback;
                this.operation = operation;
            }

            public AttributeCallback(IBMMobilePush.Droid.API.Attribute.Attribute attribute, AttributeOperation operation, AttributeResultsDelegate callback)
            {
                this.callback = callback;
                this.attribute = attribute;
                this.operation = operation;
            }

            public void OnFailure(Java.Lang.Object p0, OperationResult p1)
            {
                if (key != null)
                    callback(false, key, null, operation);
                else
                    callback(false, attribute.Key, attribute.Value, operation);
            }

            public void OnSuccess(Java.Lang.Object p0, OperationResult p1)
            {
                if (key != null)
                    callback(true, key, null, operation);
                else
                    callback(true, attribute.Key, attribute.Value, operation);
            }
        }

        [System.Obsolete("SetUserAttribute is deprecated")]
        public void SetUserAttribute<T>(string key, T value, AttributeResultsDelegate callback)
        {
            IBMMobilePush.Droid.API.Attribute.Attribute attribute = AttributeConverter(key, value);
            if (attribute != null)
            {
                var attributeCallback = new AttributeCallback(attribute, AttributeOperation.SetUserAttributes, callback);
                var attributes = new List<IBMMobilePush.Droid.API.Attribute.Attribute>() { attribute };
                MceSdk.GetAttributesClient(false).SetUserAttributes(ApplicationContext, attributes, attributeCallback);
            }
            else
            {
                Logging.Error("Could not set user attribute because could not convert value type.");
            }
        }

        [System.Obsolete("UpdateUserAttribute is deprecated, please use QueueUpdateUserAttribute instead.")]
        public void UpdateUserAttribute<T>(string key, T value, AttributeResultsDelegate callback)
        {
            IBMMobilePush.Droid.API.Attribute.Attribute attribute = AttributeConverter(key, value);
            if (attribute != null)
            {
                var attributeCallback = new AttributeCallback(attribute, AttributeOperation.SetUserAttributes, callback);
                var attributes = new List<IBMMobilePush.Droid.API.Attribute.Attribute>() { attribute };
                MceSdk.GetAttributesClient(false).UpdateUserAttributes(ApplicationContext, attributes, attributeCallback);
            }
            else
            {
                Logging.Error("Could not update user attribute because could not convert value type.");
            }
        }

        [System.Obsolete("DeleteUserAttribute is deprecated, please use QueueDeleteUserAttribute instead.")]
        public void DeleteUserAttribute(string key, AttributeResultsDelegate callback)
        {
            var attributeCallback = new AttributeCallback(key, AttributeOperation.SetUserAttributes, callback);
            MceSdk.GetAttributesClient(false).DeleteUserAttributes(ApplicationContext, new List<string>() { key }, attributeCallback);
        }

        [System.Obsolete("QueueSetUserAttribute is deprecated")]
        public void QueueSetUserAttribute<T>(string key, T value)
        {
            IBMMobilePush.Droid.API.Attribute.Attribute attribute = AttributeConverter(key, value);
            if (attribute != null)
            {
                var attributes = new List<IBMMobilePush.Droid.API.Attribute.Attribute>() { attribute };
                MceSdk.QueuedAttributesClient.SetUserAttributes(ApplicationContext, attributes);
            }
            else
            {
                Logging.Error("Could not set user attribute because could not convert value type.");
            }
        }

        public void QueueUpdateUserAttribute<T>(string key, T value)
        {
            IBMMobilePush.Droid.API.Attribute.Attribute attribute = AttributeConverter(key, value);
            if (attribute != null)
            {
                var attributes = new List<IBMMobilePush.Droid.API.Attribute.Attribute>() { attribute };
                MceSdk.QueuedAttributesClient.UpdateUserAttributes(ApplicationContext, attributes);
            }
            else
            {
                Logging.Error("Could not update user attribute because could not convert value type.");
            }
        }

        public void QueueDeleteUserAttribute(string key)
        {
            MceSdk.QueuedAttributesClient.DeleteUserAttributes(ApplicationContext, new List<string>() { key });
        }

        Event GenerateEvent(string name, string type, DateTimeOffset timestamp, string attribution, string mailingId, Dictionary<string, object> attributes)
        {
            var apiAttributes = new List<IBMMobilePush.Droid.API.Attribute.Attribute>();
            foreach (KeyValuePair<string, object> attribute in attributes)
            {
                apiAttributes.Add(AttributeConverter(attribute.Key, attribute.Value));
            }

            return new Event(type, name, Utilities.ConvertDate(timestamp, new Java.Util.Date()), apiAttributes, attribution, mailingId);
        }

        [System.Obsolete("AddEvent is deprecated, use QueueAddEvent instead")]
        public void AddEvent(string name, string type, DateTimeOffset timestamp, string attribution, string mailingId, Dictionary<string, object> attributes, EventResultsDelegate callback)
        {
            var sdkEvent = GenerateEvent(name, type, timestamp, attribution, mailingId, attributes);
            MceSdk.GetEventsClient(false).SendEvent(ApplicationContext, sdkEvent, new EventCallback(name, type, timestamp, attribution, mailingId, attributes, callback));
        }

        public void QueueAddEvent(string name, string type, DateTimeOffset timestamp, string attribution, string mailingId, Dictionary<string, object> attributes, bool flush)
        {
            var sdkEvent = GenerateEvent(name, type, timestamp, attribution, mailingId, attributes);
            MceSdk.QueuedEventsClient.SendEvent(ApplicationContext, sdkEvent, flush);
        }

        // Android doesn't natively support setting badge numbers.
        public int Badge { set; get; }

        public int Icon
        {
            set
            {
                MceSdk.NotificationsClient.NotificationsPreference.SetIcon(ApplicationContext, new Java.Lang.Integer(value));
            }
            get
            {
                return MceSdk.NotificationsClient.NotificationsPreference.GetIcon(ApplicationContext);
            }
        }

        public bool IsProviderRegistered()
        {
            return MceSdk.RegistrationClient.GetRegistrationDetails(ApplicationContext).PushToken != null;
        }

        public bool IsMceRegistered()
        {
            return MceSdk.RegistrationClient.GetRegistrationDetails(ApplicationContext).ChannelId != null;
        }

        public bool IsRegistered()
        {
            return IsProviderRegistered() && IsMceRegistered();
        }

        Dictionary<string, PushAction> actionRegistry = new Dictionary<string, PushAction>();

        public void RegisterAction(string name, PushAction handler)
        {
            actionRegistry[name] = handler;
        }

        public void ExecuteAction(JObject action, JObject payload, string attribution, string mailingId, int id)
        {
            var name = action["type"].ToString();
            PushAction handler = actionRegistry[name];
            if (handler != null)
            {
                handler.HandleAction(action, payload, attribution, mailingId, id);
            }
        }

        public void PhoneHome()
        {
            ThreadPool.QueueUserWorkItem(o => IBMMobilePush.Droid.Registration.PhoneHomeManager.PhoneHome(ApplicationContext));
        }

        public void SyncInboxMessages()
        {
            InboxMessagesClient.LoadInboxMessages(ApplicationContext, new InboxCallback(InboxMessagesUpdate));
        }

        InboxMessage ConvertToInboxMessage(IBMMobilePush.Droid.Plugin.Inbox.RichContent message)
        {
            var json = message.Content.ToString();
            var content = JObject.Parse(json);

            return new InboxMessage()
            {
                InboxMessageId = message.MessageId,
                RichContentId = message.ContentId,
                ExpirationDate = Utilities.ConvertDate(message.ExpirationDate, DateTimeOffset.MaxValue),
                SendDate = Utilities.ConvertDate(message.SendDate, DateTimeOffset.MinValue),
                TemplateName = message.Template,
                Attribution = message.Attribution,
                //MailingId = message.MailingId, // It seems like mailling id should be here, but it's missing in the SDK
                IsRead = (bool)message.IsRead,
                IsDeleted = (bool)message.IsDeleted,
                Content = content
            };
        }

        public void DeleteInboxMessage(InboxMessage message)
        {
            InboxMessagesClient.DeleteMessageById(ApplicationContext, message.InboxMessageId);
        }

        public void ReadInboxMessage(InboxMessage message)
        {
            InboxMessagesClient.SetMessageReadById(ApplicationContext, message.InboxMessageId);
        }

        public void FetchInboxMessageWithRichContentId(string richContentId, InboxMessageResultsDelegate callback)
        {
            var message = InboxMessagesClient.GetInboxMessageByContentId(ApplicationContext, richContentId);
            if (message == null)
            {
                // sync db then call back.
                ThreadPool.QueueUserWorkItem(o => InboxMessagesClient.LoadInboxMessages(ApplicationContext, new FetchInboxMessageWithRichContentIdCallback(richContentId, callback, this)));
                return;
            }

            callback(ConvertToInboxMessage(message));
        }

        class FetchInboxMessageWithRichContentIdCallback : Java.Lang.Object, IOperationCallback
        {
            string RichContentId;
            InboxMessageResultsDelegate Callback;
            IBMMobilePushImpl Push;
            public FetchInboxMessageWithRichContentIdCallback(string richContentId, InboxMessageResultsDelegate callback, IBMMobilePushImpl push)
            {
                Callback = callback;
                RichContentId = richContentId;
                Push = push;
            }

            public void OnFailure(Java.Lang.Object p0, OperationResult p1)
            {
                Logging.Error("FetchInboxMessageWithRichContentIdCallback Failed.");
            }

            public void OnSuccess(Java.Lang.Object p0, OperationResult p1)
            {
                var message = InboxMessagesClient.GetInboxMessageByContentId(Android.App.Application.Context, RichContentId);
                if (message != null)
                {
                    Callback(Push.ConvertToInboxMessage(message));
                }
            }
        }

        public void FetchInboxMessage(string inboxMessageId, InboxMessageResultsDelegate callback)
        {
            var messageCursor = RichContentDatabaseHelper.GetRichContentDatabaseHelper(ApplicationContext).GetMessagesByMessageId(inboxMessageId);
            if (messageCursor.Count > 0)
            {
                messageCursor.MoveToFirst();
                var message = messageCursor.RichContent;
                callback(ConvertToInboxMessage(message));
            }
            else
            {
                var inboxMessageReference = new InboxMessageReference(null, inboxMessageId);
                InboxMessagesClient.AddMessageToLoad(inboxMessageReference);
                ThreadPool.QueueUserWorkItem(o => InboxMessagesClient.LoadInboxMessages(ApplicationContext, new InboxCallback(() =>
                {
                    messageCursor = RichContentDatabaseHelper.GetRichContentDatabaseHelper(ApplicationContext).GetMessagesByMessageId(inboxMessageId);
                    if (messageCursor.Count > 0)
                    {
                        messageCursor.MoveToFirst();
                        var message = messageCursor.RichContent;
                        callback(ConvertToInboxMessage(message));
                        InboxMessagesUpdate?.Invoke();
                    }
                    else
                    {
                        Logging.Error("Inbox Message not found in database");
                        callback(null);
                    }

                })));
            }
        }

        public void FetchInboxMessages(Action<InboxMessage[]> completion, bool ascending)
        {
            var messageCursor = RichContentDatabaseHelper.GetRichContentDatabaseHelper(ApplicationContext).Messages;

            var messages = new List<InboxMessage>();
            while (messageCursor.MoveToNext())
            {
                var message = messageCursor.RichContent;
                messages.Add(ConvertToInboxMessage(message));
            }

            completion(messages.ToArray());
        }

        public void ExecuteAction(JToken action, string attribution, string mailingId, string source, Dictionary<string, string> attributes)
        {
            var actionType = action["type"].ToString();
            if (actionType == "url" || actionType == "dial" || actionType.ToLower() == "openapp")
            {
                var androidImpl = MceNotificationActionRegistry.GetNotificationAction(actionType);
                if (androidImpl != null)
                {
                    var payload = new Dictionary<string, string>();

                    JObject actionObject = action.Value<JObject>();

                    List<string> keys = actionObject.Properties().Select(p => p.Name).ToList();

                    foreach (string key in keys)
                    {
                        payload[key] = action[key].ToString();
                    }

                    androidImpl.HandleAction(ApplicationContext, actionType, null, null, mailingId, payload, false);

                    var eventAttributes = new List<IBMMobilePush.Droid.API.Attribute.Attribute>();
                    foreach (KeyValuePair<string, string> entry in attributes)
                    {
                        eventAttributes.Add(new StringAttribute(entry.Key, entry.Value));
                    }

                    eventAttributes.Add(new StringAttribute("actionTaken", actionType));
                    var name = actionType;
                    var clickEventDetails = MceNotificationActionImpl.GetClickEventDetails(actionType);
                    if (clickEventDetails != null)
                    {
                        name = clickEventDetails.EventName;
                        var value = action["value"].ToString();
                        eventAttributes.Add(new StringAttribute(clickEventDetails.ValueName, value));
                    }
                    else
                    {
                        foreach (KeyValuePair<string, string> entry in payload)
                        {
                            eventAttributes.Add(new StringAttribute(entry.Key, entry.Value));
                        }
                    }

                    var sdkEvent = new Event(source, name, new Date(), eventAttributes, attribution, mailingId);
                    MceSdk.QueuedEventsClient.SendEvent(ApplicationContext, sdkEvent, true);
                }
                return;
            }
            else if (!actionRegistry.ContainsKey(actionType))
            {
                return;
            }

            var actionImpl = actionRegistry[actionType];
            if (actionImpl != null)
            {
                var payload = new Dictionary<string, string>();

                JObject actionObject = action.Value<JObject>();

                List<string> keys = actionObject.Properties().Select(p => p.Name).ToList();

                foreach (string key in keys)
                {
                    payload[key] = action[key].ToString();
                }

                actionImpl.HandleAction(action as JObject, action as JObject, attribution, mailingId, 0);

                var eventAttributes = new List<IBMMobilePush.Droid.API.Attribute.Attribute>();
                foreach (KeyValuePair<string, string> entry in attributes)
                {
                    eventAttributes.Add(new StringAttribute(entry.Key, entry.Value));
                }

                eventAttributes.Add(new StringAttribute("actionTaken", actionType));
                var name = actionType;
                var clickEventDetails = MceNotificationActionImpl.GetClickEventDetails(actionType);
                if (clickEventDetails != null)
                {
                    name = clickEventDetails.EventName;
                    var value = action["value"].ToString();
                    eventAttributes.Add(new StringAttribute(clickEventDetails.ValueName, value));
                }
                else
                {
                    foreach (KeyValuePair<string, string> entry in payload)
                    {
                        eventAttributes.Add(new StringAttribute(entry.Key, entry.Value));
                    }
                }

                var sdkEvent = new Event(source, name, new Date(), eventAttributes, attribution, mailingId);

                MceSdk.QueuedEventsClient.SendEvent(ApplicationContext, sdkEvent, true);
            }
        }

        public float StatusBarHeight()
        {
            return 0;
        }

        public SQLiteAsyncConnection GetConnection(string filename)
        {
            string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            var path = Path.Combine(documentsPath, filename);

            var platform = new SQLitePlatformAndroid();
            var param = new SQLiteConnectionString(path, false);
            var connection = new SQLiteAsyncConnection(() => new SQLiteConnectionWithLock(platform, param));
            return connection;
        }

        public Xamarin.Forms.Size ScreenSize()
        {
            var metrics = ApplicationContext.Resources.DisplayMetrics;
            return new Xamarin.Forms.Size(metrics.WidthPixels / metrics.Density, metrics.HeightPixels / metrics.Density);
        }

        public ISet<Geofence> GeofencesNear(double latitude, double longitude)
        {
            var geofences = new HashSet<Geofence>();

            Location location = new Location("SDK");

            location.Latitude = latitude;
            location.Longitude = longitude;


            var allGeofences = IBMMobilePush.Droid.Location.LocationManager.GetLocations(ApplicationContext, LocationPreferences.GetCurrentLocationsState(ApplicationContext).TrackedBeaconsIds);
            foreach (var aGeofence in allGeofences)
            {
                var geofence = new Geofence(aGeofence.Latitude, aGeofence.Longitude, aGeofence.Radius, aGeofence.Id);
                geofences.Add(geofence);
            }

            return geofences;
        }

        public void SyncGeofences()
        {
            LocationRetrieveService.StartLocationUpdates(ApplicationContext);
        }

        public bool GeofenceEnabled()
        {
            return ContextCompat.CheckSelfPermission(ApplicationContext, Android.Manifest.Permission.AccessFineLocation) == Permission.Granted;
        }

        public ISet<BeaconRegion> BeaconRegions()
        {
            var beaconRegions = new HashSet<BeaconRegion>();
            var trackedIBeacons = IBMMobilePush.Droid.Location.LocationManager.GetLocations(ApplicationContext, LocationPreferences.GetCurrentLocationsState(ApplicationContext).TrackedBeaconsIds);
            foreach (MceLocation location in trackedIBeacons)
            {
                IBeacon beaconLocation = (IBeacon)location;
                beaconRegions.Add(new BeaconRegion(beaconLocation.Major, null, null));
            }

            return beaconRegions;
        }

        public bool BeaconEnabled()
        {
            BluetoothAdapter bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            if (bluetoothAdapter == null)
            {
                return false;
            }
            else
            {
                if (!bluetoothAdapter.IsEnabled)
                {
                    return false;
                }
            }
            return true;
        }

        public Guid? BeaconUUID()
        {
            var uuid = IBeaconsPreferences.GetBeaconsUUID(ApplicationContext);
            if (uuid != null)
            {
                return new Guid(uuid);
            }
            return null;
        }

        public void DeleteInAppMessage(InAppMessage inAppMessage)
        {
            InAppManager.Delete(ApplicationContext, inAppMessage.Id);
        }

        public void ExecuteInAppRule(string[] rules)
        {
            var messages = InAppStorage.Find(ApplicationContext, InAppStorage.Condition.Any, InAppStorage.KeyName.Rule, rules, false);
            foreach(var message in messages) {
                var jsonObject = new JObject();

                if(message.Id != null) {
                    jsonObject.Add("inAppMessageId", message.Id);
                }

                if (message.TemplateName != null)
                {
                    jsonObject.Add("template", message.TemplateName);
                }

                jsonObject.Add("maxViews", message.MaxViews);
                jsonObject.Add("numViews", message.Views);


                Java.Util.TimeZone timeZone = Java.Util.TimeZone.GetTimeZone("UTC");
                Java.Text.DateFormat dateFormat = new Java.Text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm'Z'");
                dateFormat.TimeZone = timeZone;

                jsonObject.Add("expirationDate", dateFormat.Format(message.ExpirationDate));
                jsonObject.Add("triggerDate", dateFormat.Format(message.TriggerDate));

                var mceObject = new JObject();

                if (message.Attribution != null)
                {
                    mceObject.Add("attribution", message.Attribution);
                }

                if (message.MailingId != null)
                {
                    mceObject.Add("mailingId", message.MailingId);
                }

                jsonObject.Add("mce", mceObject);

                jsonObject.Add("content", JObject.Parse(message.TemplateContent.ToString()));
                jsonObject.Add("rules", new JArray(message.Rules));

                var inAppMessage = new InAppMessage(jsonObject);
                if (inAppMessage.Execute())
                {
                    InAppEvents.SendInAppMessageOpenedEvent(ApplicationContext, message);
                    InAppStorage.UpdateMaxViews(ApplicationContext, message);
                    if (message.IsFromPull)
                    {
                        InAppPlugin.UpdateInAppMessage(ApplicationContext, message);
                    }
                    return;
                }
            }
        }

        public void InsertInAppAsync(InAppMessage message)
        {
            var msgExtras = new Bundle();
            msgExtras.PutString("inApp", message.JsonObject().ToString());
            InAppManager.HandleNotification(ApplicationContext, msgExtras, null, null);
        }

        public Thickness SafeAreaInsets()
        {
            return new Thickness();
        }

        public bool UserInvalidated()
        {
            return IBMMobilePush.Droid.Registration.RegistrationPreferences.IsSdkStopped(ApplicationContext);
        }

        class InboxCallback : Java.Lang.Object, IOperationCallback
        {
            GenericDelegate Callback;
            public InboxCallback(GenericDelegate callback)
            {
                Callback = callback;
            }
            public void OnFailure(Java.Lang.Object p0, OperationResult p1)
            {
                Logging.Error("InboxCallback Failed.");
            }

            public void OnSuccess(Java.Lang.Object p0, OperationResult p1)
            {
                Callback();
            }
        }

        class EventCallback : Java.Lang.Object, IOperationCallback
        {
            string Name;
            string Type;
            DateTimeOffset Timestamp;
            string MailingId;
            string Attribution;
            Dictionary<string, object> Attributes;
            EventResultsDelegate Callback;

            public EventCallback(string name, string type, DateTimeOffset timestamp, string attribution, string mailingId, Dictionary<string, object> attributes, EventResultsDelegate callback)
            {
                Name = name;
                Type = type;
                Timestamp = timestamp;
                Attribution = attribution;
                MailingId = mailingId;
                Attributes = attributes;
                Callback = callback;
            }

            public void OnFailure(Java.Lang.Object p0, OperationResult p1)
            {
                Callback(false, Name, Type, Timestamp, Attribution, MailingId, Attributes);
            }

            public void OnSuccess(Java.Lang.Object p0, OperationResult p1)
            {
                Callback(true, Name, Type, Timestamp, Attribution, MailingId, Attributes);
            }
        }
    }

    [BroadcastReceiver(Enabled = true)]
    [IntentFilter(new[] {"com.ibm.mce.sdk.EXECUTE_CUSTOM_ACTION"})]
    class ActionBroadcastReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            var jsonActionString = intent.GetStringExtra("action");
            if (jsonActionString != null)
            {

                var jsonAction = JObject.Parse(jsonActionString);
                JObject jsonPayload = null;
                if (intent.HasExtra("payload"))
                {
                    jsonPayload = JObject.Parse(intent.GetStringExtra("payload"));
                }
                var attribution = intent.GetStringExtra("attribution");
                int id = 0;
                try
                {
                    id = Int32.Parse(intent.GetStringExtra("id"));
                }
                catch { }

                var mailingId = intent.GetStringExtra("mailingId");

                SDK.Instance.ExecuteAction(jsonAction, jsonPayload, attribution, mailingId, id);
            }
        }
    }

    [BroadcastReceiver(Enabled = true)]
    [IntentFilter(new[] { "com.ibm.mce.sdk.NOTIFIER" })]
	class IBMMobilePushBroadcastReceiver : MceBroadcastReceiver
	{
		public override void OnLocationEvent(Context context, MceLocation location, EventBroadcastHandlerLocationType locationType, EventBroadcastHandlerLocationEventType eventType)
		{
			if (locationType == EventBroadcastHandlerLocationType.Ibeacon)
			{
				var iBeacon = location as IBeacon;
				if (iBeacon != null)
				{
                    if (eventType == EventBroadcastHandlerLocationEventType.Enter)
                        SDK.Instance.BeaconEntered?.Invoke(new BeaconRegion(iBeacon.Major, iBeacon.Minor, iBeacon.Id));
					if (eventType == EventBroadcastHandlerLocationEventType.Exit)
                        SDK.Instance.BeaconExited?.Invoke(new BeaconRegion(iBeacon.Major, iBeacon.Minor, iBeacon.Id));					}
			}
			if (locationType == EventBroadcastHandlerLocationType.Geofence)
			{
				var geofence = location as MceGeofence;
				if (geofence != null)
				{
					if (eventType == EventBroadcastHandlerLocationEventType.Enter)
                        SDK.Instance.GeofenceEntered?.Invoke(new Geofence(geofence.Latitude, geofence.Longitude, geofence.Radius, geofence.Id));
					if (eventType == EventBroadcastHandlerLocationEventType.Exit)
                        SDK.Instance.GeofenceExited?.Invoke(new Geofence(geofence.Latitude, geofence.Longitude, geofence.Radius, geofence.Id));
				}
			}
		}

		public override void OnLocationUpdate(Context context, Location location)
		{
            SDK.Instance.LocationsUpdated?.Invoke();
		}

		public override void OnSdkRegistered (Context p0)
		{
            SDK.Instance.RegistrationUpdated?.Invoke();
		}
		public override void OnAttributesOperation (Context p0, IAttributesOperation p1)
		{
            if (SDK.Instance.AttributeQueueResults != null)
            {
                var reportOperationInt = p1.Type.Ordinal();
                AttributeOperation reportOperation = (AttributeOperation)reportOperationInt;
                if (p1.AttributeKeys != null)
                {
                    foreach (var key in p1.AttributeKeys)
                    {
                        SDK.Instance.AttributeQueueResults(true, key, null, reportOperation);
                    }
                }
                if (p1.Attributes != null)
                {
                    foreach (var attribute in p1.Attributes)
                    {
                        SDK.Instance.AttributeQueueResults(true, attribute.Key, attribute.Value, reportOperation);
                    }
                }
            }
        }
		public override void OnC2dmError (Context p0, string p1)
		{
		}
		public override void OnDeliveryChannelRegistered (Context p0)
		{
		}
		public override void OnEventsSend (Context p0, IList<Event> p1)
		{
            if (SDK.Instance.EventQueueResults != null)
            {
                foreach (var apiEvent in p1)
                {
                    var name = apiEvent.Name;
                    var type = apiEvent.Type;
                    var timestamp = apiEvent.Timestamp;
                    var attribution = apiEvent.Attribution;
                    var mailingId = apiEvent.MailingId;
                    var attributes = new Dictionary<string, object>();
                    foreach (var attribute in apiEvent.Attributes)
                    {
                        attributes.Add(attribute.Key, attribute.Value);
                    }
                    SDK.Instance.EventQueueResults(true, name, type, Utilities.ConvertDate(timestamp, DateTimeOffset.Now), attribution, mailingId, attributes);
                }
            }
		}
		public override void OnIllegalNotification (Context p0, Intent p1)
		{
		}

		public override void OnMessage (Context context, INotificationDetails notificationDetails, Bundle bundle)
		{
            if (!bundle.ContainsKey("inApp"))
            {
                return;
            }

            var msgExtras = new Bundle();
            msgExtras.PutString("inApp", bundle.GetString("inApp"));
            InAppManager.HandleNotification(Android.App.Application.Context, msgExtras, null, null);
		}
		public override void OnNonMceBroadcast (Context p0, Intent p1)
		{
		}
		public override void OnNotificationAction (Context p0, Date p1, string p2, string p3, string p4)
		{
		}
		public override void OnSdkRegistrationChanged (Context p0)
		{
            SDK.Instance.RegistrationUpdated?.Invoke();
		}
		public override void OnSessionEnd (Context p0, Date p1, long p2)
		{
		}

		public override void OnSessionStart(Context p0, Date p1)
		{
		}
	}

}

