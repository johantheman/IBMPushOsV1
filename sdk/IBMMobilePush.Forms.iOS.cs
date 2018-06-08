/*
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

using Xamarin.Forms;

using IBMMobilePush.iOS;

using UIKit;
using Foundation;
using ObjCRuntime;
using CoreLocation;

using Newtonsoft.Json.Linq;

using SQLite.Net;
using SQLite.Net.Async;
using SQLite.Net.Platform.XamarinIOS;

[assembly: Dependency(typeof(IBMMobilePush.Forms.iOS.IBMMobilePushImpl))]
namespace IBMMobilePush.Forms.iOS
{
	public class IBMMobilePushImpl : IIBMMobilePush
	{
        public void InsertInAppAsync(InAppMessage message)
        {
            var inAppJsonString = message.JsonObject().ToString();
            var inAppJsonData = NSData.FromString(inAppJsonString);

            var payload = new NSMutableDictionary();

            NSError error = null;
            payload["inApp"] = (NSDictionary)NSJsonSerialization.Deserialize(inAppJsonData, NSJsonReadingOptions.AllowFragments, out error);

            if(error != null) {
                return;
            }

            MCEInAppManager.SharedInstance().ProcessPayload(payload);
        }

        public void DeleteInAppMessage(InAppMessage inAppMessage)
        {
            var mceInAppMessage = MCEInAppManager.SharedInstance().InAppMessageById(inAppMessage.Id);
            MCEInAppManager.SharedInstance().Disable(mceInAppMessage);
        }

        public void ExecuteInAppRule(string[] rules)
        {
            MCEInAppManager.SharedInstance().FetchInAppMessagesForRules(rules, (messages, error) =>
            {
                if(error != null) {
                    return;
                }

                foreach(var message in messages) {
                    NSError jsonError = null;

                    var jsonDictionary = new NSMutableDictionary();
                    if(message.InAppMessageId != null) {
                        jsonDictionary.Add(new NSString("inAppMessageId"), new NSString(message.InAppMessageId));
                    }

                    if (message.TemplateName != null)
                    {
                        jsonDictionary.Add(new NSString("template"), new NSString(message.TemplateName));
                    }
                    jsonDictionary.Add(new NSString("maxViews"), new NSNumber(message.MaxViews));
                    jsonDictionary.Add(new NSString("numViews"), new NSNumber(message.NumViews));
                    jsonDictionary.Add(new NSString("expirationDate"), new NSString(MCEApiUtil.DateToIso8601Format(message.ExpirationDate)));
                    jsonDictionary.Add(new NSString("triggerDate"), new NSString(MCEApiUtil.DateToIso8601Format(message.TriggerDate)));

                    var mceDictionary = new NSMutableDictionary();

                    if (message.Attribution != null)
                    {
                        mceDictionary.Add(new NSString("attribution"), new NSString(message.Attribution));
                    }

                    if (message.MailingId != null)
                    {
                        mceDictionary.Add(new NSString("mailingId"), new NSString(message.MailingId));
                    }
                    jsonDictionary.Add(new NSString("mce"), mceDictionary);

                    jsonDictionary.Add(new NSString("content"), message.Content);
                    jsonDictionary.Add(new NSString("rules"), message.Rules);

                    var jsonData = NSJsonSerialization.Serialize(jsonDictionary, NSJsonWritingOptions.PrettyPrinted, out jsonError);
                    var jsonString = NSString.FromData(jsonData, NSStringEncoding.UTF8);
                    var jsonObject = JObject.Parse(jsonString);

                    var inAppMessage = new InAppMessage(jsonObject);
                    if(inAppMessage.Execute()) {
                        MCEInAppManager.SharedInstance().IncrementView(message);
                        return;
                    }
                }
            });
        }

        CLLocationManager LocationManager;

        public String XamarinPluginVersion() {
            return null;
        }
		public IBMMobilePushImpl()
        {
			NSNotificationCenter.DefaultCenter.AddObserver(new NSString("EnteredGeofence"), (note) =>
			{
				CLCircularRegion region = note.UserInfo["region"] as CLCircularRegion;
				if (GeofenceEntered != null)
				{
					var geofence = new Geofence(region.Center.Latitude, region.Center.Longitude, region.Radius, region.Identifier);
					GeofenceEntered(geofence);
				}
			});

			NSNotificationCenter.DefaultCenter.AddObserver(new NSString("ExitedGeofence"), (note) =>
			{
				CLCircularRegion region = note.UserInfo["region"] as CLCircularRegion;
				if (GeofenceExited != null)
				{
					var geofence = new Geofence(region.Center.Latitude, region.Center.Longitude, region.Radius, region.Identifier);
					GeofenceExited(geofence);
				}
			});

			NSNotificationCenter.DefaultCenter.AddObserver(new NSString("EnteredBeacon"), (note) =>
			{
				if (BeaconEntered != null)
				{
					var major = note.UserInfo["major"] as NSNumber;
					var minor = note.UserInfo["minor"] as NSNumber;
					var locationId = note.UserInfo["locationId"] as NSString;
					var beaconRegion = new BeaconRegion(major.Int32Value, minor.Int32Value, locationId);
					BeaconEntered(beaconRegion);
				}
			});

			NSNotificationCenter.DefaultCenter.AddObserver(new NSString("ExitedBeacon"), (note) =>
			{
				if (BeaconExited != null)
				{
					var major = note.UserInfo["major"] as NSNumber;
					var minor = note.UserInfo["minor"] as NSNumber;
					var locationId = note.UserInfo["locationId"] as NSString;
					var beaconRegion = new BeaconRegion(major.Int32Value, minor.Int32Value, locationId);
					BeaconExited(beaconRegion);
				}
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("MCERegistrationChangedNotification"), RegistrationUpdatedNotification);
            NSNotificationCenter.DefaultCenter.AddObserver (new NSString("MCERegisteredNotification"), RegistrationUpdatedNotification);
		
			NSNotificationCenter.DefaultCenter.AddObserver(new NSString("LocationDatabaseUpdated"), (note) =>
			{
                LocationsUpdated?.Invoke();
            });

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("SetUserAttributesSuccess"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.SetUserAttributes, true, note);
			});
		
			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("UpdateUserAttributesSuccess"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.UpdateUserAttributes, true, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("DeleteUserAttributesSuccess"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.DeleteUserAttributes, true, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("SetChannelAttributesSuccess"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.SetChannelAttributes, true, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("UpdateChannelAttributesSuccess"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.UpdateChannelAttributes, true, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("DeleteChannelAttributesSuccess"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.DeleteChannelAttributes, true, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("SetUserAttributesError"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.SetUserAttributes, false, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("UpdateUserAttributesError"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.UpdateUserAttributes, false, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("DeleteUserAttributesError"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.DeleteUserAttributes, false, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("SetChannelAttributesError"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.SetChannelAttributes, false, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("UpdateChannelAttributesError"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.UpdateChannelAttributes, false, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("DeleteChannelAttributesError"), (note) => {
				AttributeQueueResultNotification(AttributeOperation.DeleteChannelAttributes, false, note);
			});

			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("MCEEventFailure"), (note) => {
				EventQueueResultNotification(false, note);
			});
			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("MCEEventSuccess"),(note) => {
				EventQueueResultNotification(true, note);
			});


			NSNotificationCenter.DefaultCenter.AddObserver (new NSString("MCESyncDatabase"),(note) => {
                InboxMessagesUpdate?.Invoke();
            });

            LocationManager = new CLLocationManager();
            LocationManager.AuthorizationChanged += (sender, e) => {
                if (LocationAuthorizationChanged != null)
                    LocationAuthorizationChanged();
            };

		}

		public bool LocationInitialized()
		{
            return CLLocationManager.Status == CLAuthorizationStatus.AuthorizedAlways;
		}

        public GenericDelegate LocationAuthorizationChanged { set; get; }

		public GeofenceDelegate GeofenceEntered { set; get; }
		public GeofenceDelegate GeofenceExited { set; get; }
		public BeaconDelegate BeaconExited { set; get; }
		public BeaconDelegate BeaconEntered { set; get; }
		

		public RegistrationUpdatedDelegate RegistrationUpdated { set; get; }
		public AttributeResultsDelegate AttributeQueueResults { set; get; }
		public EventResultsDelegate EventQueueResults { set; get; }
		public GenericDelegate InboxMessagesUpdate { set; get; }
		public GenericDelegate LocationsUpdated { get; set; }

		public void AttributeQueueResultNotification (AttributeOperation operation, bool success, NSNotification note)
		{
			if(AttributeQueueResults != null)
			{
				NSDictionary attributesDict = (NSDictionary) note.UserInfo["attributes"];
				NSArray keys = (NSArray) note.UserInfo["keys"];
				if(keys != null)
				{
					for(nuint i=0;i<keys.Count;i++)
					{
						NSString key = keys.GetItem<NSString> (i);
						AttributeQueueResults(success, key, null, operation);
					}

				}
				else if(attributesDict != null)
				{
					foreach(NSString key in attributesDict.Keys)
					{
						NSObject val = attributesDict[key];
						AttributeQueueResults(success, key, val, operation);
					}
				}

			}
		}

		public void RegistrationUpdatedNotification(NSNotification note)
		{
            RegistrationUpdated?.Invoke();
        }

		public String Version()
		{
			return MCESdk.SharedInstance().SdkVersion;
		}

		public String UserId()
		{
            return MCERegistrationDetails.SharedInstance().UserId;
		}

		public String ChannelId()
		{
			return MCERegistrationDetails.SharedInstance().ChannelId;
		}

		public String AppKey()
		{
			return MCESdk.SharedInstance().Config.AppKey;
		}


		MCEAttributesClient _AttributeClient;
        [System.Obsolete("MCEAttributesClient is deprecated")]
		MCEAttributesClient AttributeClient { 
			get {
				if (_AttributeClient == null) {
					_AttributeClient = new MCEAttributesClient ();
				}
				return _AttributeClient;
			} 
		}

        [System.Obsolete("SetUserAttribute is deprecated")]
		public void SetUserAttribute<T> (String key, T value, AttributeResultsDelegate callback)
		{
			AttributeClient.SetUserAttributes(new NSDictionary(key, value), (error) => {
				callback(error == null, key, value, AttributeOperation.SetUserAttributes);
			});
		}

        [System.Obsolete("UpdateUserAttribute is deprecated, please use QueueUpdateUserAttribute instead.")]
		public void UpdateUserAttribute<T> (string key, T value, AttributeResultsDelegate callback)
		{
			AttributeClient.UpdateUserAttributes(new NSDictionary(key, value), (error) => {
				callback(error == null, key, value, AttributeOperation.UpdateUserAttributes);
			});
		}

        [System.Obsolete("DeleteUserAttribute is deprecated, please use QueueDeleteUserAttribute instead.")]
		public void DeleteUserAttribute (string key, AttributeResultsDelegate callback)
		{
			AttributeClient.DeleteUserAttributes(new NSObject[] { (NSString) key }, (error) => {
				callback(error == null, key, null, AttributeOperation.DeleteUserAttributes);
			});
		}

        [System.Obsolete("SetChannelAttribute is deprecated")]
		public void SetChannelAttribute<T> (string key, T value, AttributeResultsDelegate callback)
		{
			AttributeClient.SetChannelAttributes(new NSDictionary(key, value), (error) => {
				callback(error == null, key, value, AttributeOperation.SetChannelAttributes);
			});
		}

        [System.Obsolete("UpdateChannelAttribute is deprecated")]
		public void UpdateChannelAttribute<T> (string key, T value, AttributeResultsDelegate callback)
		{
			AttributeClient.UpdateChannelAttributes(new NSDictionary(key, value), (error) => {
				callback(error == null, key, value, AttributeOperation.UpdateChannelAttributes);
			});
		}

        [System.Obsolete("DeleteChannelAttribute is deprecated")]
		public void DeleteChannelAttribute (string key, AttributeResultsDelegate callback)
		{
			AttributeClient.DeleteChannelAttributes(new NSObject[] { (NSString) key }, (error) => {
				callback(error == null, key, null, AttributeOperation.DeleteChannelAttributes);
			});
		}

        [System.Obsolete("QueueSetUserAttribute is deprecated")]
		public void QueueSetUserAttribute<T> (string key, T value)
		{
			MCEAttributesQueueManager.SharedInstance ().SetUserAttributes (new NSDictionary (key, value));
		}

		public void QueueUpdateUserAttribute<T> (string key, T value)
		{
			MCEAttributesQueueManager.SharedInstance ().UpdateUserAttributes (new NSDictionary (key, value));
		}

		public void QueueDeleteUserAttribute (string key)
		{
			MCEAttributesQueueManager.SharedInstance ().DeleteUserAttributes (new NSObject[] { (NSString) key } );
		}

        [System.Obsolete("QueueSetChannelAttribute is deprecated")]
		public void QueueSetChannelAttribute<T> (string key, T value)
		{	
			MCEAttributesQueueManager.SharedInstance ().SetChannelAttributes (new NSDictionary (key, value));
		}

        [System.Obsolete("QueueUpdateChannelAttribute is deprecated")]
		public void QueueUpdateChannelAttribute<T> (string key, T value)
		{
			MCEAttributesQueueManager.SharedInstance ().UpdateChannelAttributes (new NSDictionary (key, value));
		}

        [System.Obsolete("QueueDeleteChannelAttribute is deprecated")]
		public void QueueDeleteChannelAttribute (string key)
		{
			MCEAttributesQueueManager.SharedInstance ().DeleteChannelAttributes (new NSObject[] { (NSString) key } );
		}

		public void QueueAddEvent (string name, string type, DateTimeOffset timestamp, string attribution, string mailingId, Dictionary<string,object> attributes, bool flush)
		{
			var apiEvent = GenerateEvent (name, type, timestamp, attribution, mailingId, attributes);
			MCEEventService.SharedInstance ().AddEvent (apiEvent, flush);
		}

		MCEEventClient _EventClient;
		MCEEventClient EventClient { 
			get {
				if (_EventClient == null) {
					_EventClient = new MCEEventClient ();
				}
				return _EventClient;
			} 
		}

		public static DateTimeOffset ConvertDate(NSDate date, DateTimeOffset defaultValue)
		{					
			if(date != null)
			{
				DateTime reference = TimeZone.CurrentTimeZone.ToLocalTime( new DateTime(2001, 1, 1, 0, 0, 0) );
				return reference.AddSeconds(date.SecondsSinceReferenceDate);
			}
			return defaultValue;
		}

		public static NSDate ConvertDate(DateTimeOffset timestamp, NSDate defaultValue)
		{
			if (timestamp != default(DateTimeOffset)) {
				DateTime reference = TimeZone.CurrentTimeZone.ToLocalTime (new DateTime (2001, 1, 1, 0, 0, 0));
				return NSDate.FromTimeIntervalSinceReferenceDate ((timestamp - reference).TotalSeconds);
			}
			return defaultValue;
		}

		MCEEvent GenerateEvent<T>(string name, string type, DateTimeOffset timestamp, string attribution, string mailingId, Dictionary<string,T> attributes)
		{
			var apiEvent = new MCEEvent ();
			apiEvent.Name = name;
			apiEvent.Type = type;

			if (timestamp != default(DateTimeOffset)) {
				apiEvent.Timestamp = ConvertDate (timestamp, NSDate.Now);
			}

			if (attribution != null)
				apiEvent.Attribution = attribution;

			if (mailingId != null)
				apiEvent.MailingId = mailingId;

			if (attributes != null) {
				var mutableAttributes = new NSMutableDictionary ();
				foreach (KeyValuePair<string, T> attribute in attributes) {
					mutableAttributes.Add (NSObject.FromObject (attribute.Key), NSObject.FromObject (attribute.Value));
				}
				apiEvent.Attributes = mutableAttributes;
			}

			return apiEvent;
		}

        [System.Obsolete("AddEvent is deprecated, use QueueAddEvent instead")]
		public void AddEvent(string name, string type, DateTimeOffset timestamp, string attribution, string mailingId, Dictionary<string,object> attributes, EventResultsDelegate callback)
		{
			var apiEvent = GenerateEvent (name, type, timestamp, attribution, mailingId, attributes);
			EventClient.SendEvents (new MCEEvent[] { apiEvent }, delegate(NSError error) {
				callback(error == null, name, type, timestamp, attribution, mailingId, attributes);
			});
		}

		void EventQueueResultNotification(bool success, NSNotification note)
		{
			if (EventQueueResults == null)
				return;
			
			NSArray apiEvents = (NSArray) note.UserInfo["events"];
			if(apiEvents != null)
			{
				for(nuint i=0;i<apiEvents.Count;i++)
				{
					MCEEvent apiEvent = apiEvents.GetItem<MCEEvent> (i);
					string name = apiEvent.Name;
					string type = apiEvent.Type;

					DateTimeOffset timestamp = ConvertDate (apiEvent.Timestamp, default(DateTimeOffset));
					string attribution = apiEvent.Attribution;
					string mailingId = apiEvent.MailingId;
					Dictionary<string,object> attributes = new Dictionary<string, object> ();

					NSDictionary attributesDict = apiEvent.Attributes;
					if(attributesDict != null)
					{
						var keys = attributesDict.Keys;
						if(keys != null)
						{
							for(int ii=0;ii<keys.Length;ii++)
							{
								NSString key = (NSString) keys[ii];
								attributes.Add(key, attributesDict.ObjectForKey(key));
							}
						}
					}

					EventQueueResults(success, name, type, timestamp, attribution, mailingId, attributes);
				}

			}
		}

		public int Badge { 
			set {
				UIApplication.SharedApplication.ApplicationIconBadgeNumber = value;
			}
			get { 
				return (int) UIApplication.SharedApplication.ApplicationIconBadgeNumber;
			}
		}

		// iOS doesn't natively support setting icons.
		public int Icon { set; get; }

		public bool IsProviderRegistered()
		{
			return MCERegistrationDetails.SharedInstance().ApsRegistered;
		}

		public bool IsMceRegistered()
		{
			return MCERegistrationDetails.SharedInstance().MceRegistered;
		}

		public bool IsRegistered()
		{
			return IsProviderRegistered () && IsMceRegistered ();
		}

		public void RegisterAction (string name, PushAction handler)
		{
			MCEActionRegistry.SharedInstance ().RegisterTarget (new ActionHandler (handler), new Selector("handleAction:payload:"), name);
		}

		public class ActionHandler : NSObject
		{
			PushAction Handler;

			public ActionHandler(PushAction handler)
			{
				Handler = handler;
			}

			[Export ("handleAction:payload:")]
			public void HandleAction(NSDictionary action, NSDictionary payload)
			{
				string attribution = null;
				string mailingId = null;
				NSDictionary mce = (NSDictionary) payload["mce"];
				if (mce != null) {
					if (mce.ContainsKey(new NSString("attribution")))
						attribution = (NSString)mce ["attribution"];
					if (mce.ContainsKey(new NSString("mailingId")))
						mailingId = (NSString)mce["mailingId"];
				}
				NSError error = null;
				var jsonPayloadData = NSJsonSerialization.Serialize (payload, NSJsonWritingOptions.PrettyPrinted, out error);
				var jsonPayloadString = NSString.FromData (jsonPayloadData, NSStringEncoding.UTF8);
				var jsonPayloadObject = JObject.Parse (jsonPayloadString);

				var jsonActionData = NSJsonSerialization.Serialize (action, NSJsonWritingOptions.PrettyPrinted, out error);
				var jsonActionString = NSString.FromData (jsonActionData, NSStringEncoding.UTF8);
				var jsonActionObject = JObject.Parse (jsonActionString);

				Handler.HandleAction (jsonActionObject, jsonPayloadObject, attribution, mailingId, 0);
			}
		}

		// iOS Support only
		public void ManualLocationInitialization()
		{
            MCESdk.SharedInstance().ManualLocationInitialization();
		}

		// iOS Support only
		public void ManualSdkInitialization()
		{
            MCESdk.SharedInstance().ManualInitialization();
		}

		public void PhoneHome()
		{
			MCEPhoneHomeManager.ForcePhoneHome ();
		}

		public void SyncInboxMessages()
		{
			MCEInboxQueueManager.SharedInstance ().SyncInbox ();
		}

		public static InboxMessage Convert(MCEInboxMessage inboxMessage)
		{
			DateTimeOffset expirationDate = ConvertDate(inboxMessage.ExpirationDate, DateTimeOffset.MaxValue);
			DateTimeOffset sendDate = DateTimeOffset.MinValue;
			if(inboxMessage.SendDate != null)
			{
				DateTime reference = TimeZone.CurrentTimeZone.ToLocalTime( new DateTime(2001, 1, 1, 0, 0, 0) );
				sendDate = reference.AddSeconds(inboxMessage.SendDate.SecondsSinceReferenceDate);
			}


            NSError error = null;
            var contentString = new NSString( NSJsonSerialization.Serialize(inboxMessage.Content,0, out error), NSStringEncoding.UTF8);
            if(error != null)
            {
                return null;
            }
			var content = JObject.Parse(contentString);

			return new InboxMessage () {
				InboxMessageId = inboxMessage.InboxMessageId,
				RichContentId = inboxMessage.RichContentId,
				ExpirationDate = expirationDate,
				SendDate = sendDate,
				TemplateName = inboxMessage.TemplateName,
				Attribution = inboxMessage.Attribution,
				MailingId = inboxMessage.MailingId,
				IsRead=inboxMessage.IsRead,
				IsDeleted=inboxMessage.IsDeleted,
				Content = content
			};
		}

        public void FetchInboxMessage(string inboxMessageId, InboxMessageResultsDelegate callback)
        {
            MCEInboxMessage inboxMessage = MCEInboxDatabase.SharedInstance().InboxMessageWithInboxMessageId(inboxMessageId);

            if (inboxMessage == null)
            {
                MCEInboxQueueManager.SharedInstance().GetInboxMessageId(inboxMessageId, (MCEInboxMessage arg0, NSError arg1) =>
                {
                    if (inboxMessage == null)
                    {
                        callback(null);
                        return;
                    }
                    else
                    {
                        callback(Convert(inboxMessage));
                    }
                });
            }
            else
            {
                callback(Convert(inboxMessage));
            }
 		}

		public void ExecuteAction(JToken action, string attribution, string mailingId, string source, Dictionary<string, string> attributes)
		{
			var actionString = action.ToString ();
			var actionData = NSData.FromString (actionString);
			NSError error = null;
			var actionDict = (NSDictionary) NSJsonSerialization.Deserialize(actionData, 0, out error);
			var payload = new NSDictionary ("mce", new NSDictionary ("attribution", attribution, "mailingId", mailingId));

			var attributesDict = new NSMutableDictionary();
			foreach (KeyValuePair<string, string> entry in attributes)
			{
				attributesDict.Add( new NSString( entry.Key ), new NSString(entry.Value));
			}
			MCEActionRegistry.SharedInstance ().PerformAction (actionDict, payload, source, attributesDict);
		}

		public void DeleteInboxMessage(InboxMessage message)
		{
			message.IsDeleted = true;
			MCEInboxMessage inboxMessage = MCEInboxDatabase.SharedInstance().InboxMessageWithInboxMessageId(message.InboxMessageId);
			inboxMessage.IsDeleted = true;
		}

		public void ReadInboxMessage (InboxMessage message)
		{
			message.IsRead = true;
			MCEInboxMessage inboxMessage = MCEInboxDatabase.SharedInstance().InboxMessageWithInboxMessageId(message.InboxMessageId);
			inboxMessage.IsRead = true;
		}

		InboxMessageResultsDelegate fetchCallback;
		string richContentIdCallback;
		public void FetchInboxMessageWithRichContentId(string richContentId, InboxMessageResultsDelegate callback)
		{
			MCEInboxMessage inboxMessage = MCEInboxDatabase.SharedInstance().InboxMessageWithRichContentId(richContentId);

			if (inboxMessage == null)
			{
				richContentIdCallback = richContentId;
				fetchCallback = callback;
				InboxMessagesUpdate += CallbackFetchInboxMessageWithRichContentId;
				SyncInboxMessages();
			}
			else
			{
				callback(Convert(inboxMessage));
			}

		}

		void CallbackFetchInboxMessageWithRichContentId()
		{
			InboxMessagesUpdate -= CallbackFetchInboxMessageWithRichContentId;
			if (richContentIdCallback != null)
			{
				FetchInboxMessageWithRichContentId(richContentIdCallback, fetchCallback);
				richContentIdCallback = null;
				fetchCallback = null;
			}
		}

		public void FetchInboxMessages (Action<InboxMessage[]> completion, bool ascending)
		{
			NSMutableArray messages = MCEInboxDatabase.SharedInstance().InboxMessagesAscending(ascending);
			var messageList = new List<InboxMessage>();
			for(nuint i=0;i< messages.Count; i++)
			{
				var message = messages.GetItem<MCEInboxMessage>(i);
				messageList.Add(Convert(message));
			}
			completion(messageList.ToArray());
		}

		public float StatusBarHeight()
		{
			return (float) (UIApplication.SharedApplication.StatusBarFrame.Height);
		}

		public Size ScreenSize()
		{
			var size = UIScreen.MainScreen.Bounds.Size;
			return new Size (size.Width, size.Height);
		}

		public SQLiteAsyncConnection GetConnection(string filename)
		{
			string documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
			string libraryPath = Path.Combine (documentsPath, "..", "Library");
			var path = Path.Combine(libraryPath, filename);

			var platform = new SQLitePlatformIOS();
			var param = new SQLiteConnectionString(path, false);
			var connection = new SQLiteAsyncConnection(() => new SQLiteConnectionWithLock(platform, param));
			return connection;
		}

		public void ExecuteAction(JObject action, JObject payload, string attribution, string mailingId, int id)
		{
			// only used in Android
		}

		public bool GeofenceEnabled()
		{
			return MCESdk.SharedInstance().Config.GeofenceEnabled;
		}
		
		public ISet<Geofence> GeofencesNear(double latitude, double longitude)
		{
            CLLocationCoordinate2D coords = new CLLocationCoordinate2D(latitude, longitude);
            var cl_geofences = MCELocationDatabase.SharedInstance().GeofencesNear(coords, 10000);
			var geofences = new HashSet<Geofence>();
			foreach (MCEGeofence region in cl_geofences)
			{
                geofences.Add(new Geofence(region.Coordinate.Latitude, region.Coordinate.Longitude, region.Radius, region.LocationId));
			}
			return geofences;
		}

		public void SyncGeofences()
		{
			new MCELocationClient().ScheduleSync();
		}

		public ISet<BeaconRegion> BeaconRegions()
		{
			var beaconRegions = MCELocationDatabase.SharedInstance().BeaconRegions();
			var beacons = new HashSet<BeaconRegion>();
            if(beaconRegions != null)
            {
				foreach (CLBeaconRegion beaconRegion in beaconRegions)
				{
					beacons.Add(new BeaconRegion(beaconRegion.Major.Int32Value, null, beaconRegion.Identifier));
				}
			}
			return beacons;
		}

		public bool BeaconEnabled()
		{
			return MCESdk.SharedInstance().Config.BeaconEnabled;
		}

		public Guid? BeaconUUID()
		{
			var uuid = MCESdk.SharedInstance().Config.BeaconUUID;
			if (uuid != null)
			{
				return new Guid(MCESdk.SharedInstance().Config.BeaconUUID.AsString());
			}
			return null;
		}

        public Thickness SafeAreaInsets() {
            if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0)) {
                var insets = UIApplication.SharedApplication.KeyWindow.SafeAreaInsets;
                return new Thickness(insets.Left, insets.Top, insets.Right, insets.Bottom);
            }
            else {
                return new Thickness(0, 10, 0, 0);
            }
        }

        public bool UserInvalidated()
        {
            return MCERegistrationDetails.SharedInstance().UserInvalidated;
        }
    }

	static class Utility
	{
		public static UIViewController FindCurrentViewController()
		{
			return FindCurrentViewController (UIApplication.SharedApplication.KeyWindow.RootViewController);
		}

		static UIViewController FindCurrentViewController(UIViewController viewController)
		{
			UINavigationController navController = viewController as UINavigationController;
			UITabBarController tabController = viewController as UITabBarController;

			if (navController != null) {
				return FindCurrentViewController(navController.VisibleViewController);
			} else if (tabController != null) {
				return FindCurrentViewController (tabController.SelectedViewController);
			} else {
				if (viewController.PresentedViewController != null) {
					return FindCurrentViewController(viewController.PresentedViewController);
				} else {
					return viewController;
				}
			}
		}

	}
}

