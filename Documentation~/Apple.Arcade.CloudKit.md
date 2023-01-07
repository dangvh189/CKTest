# Apple Arcade - Cloud Kit Wrapper

## Topics
1. [Integration Tests](#integration-tests)
2. [Apple Documentation](#apple-documentation)
3. [Usage](#usage)
    - [Container](#1-container)
    - [Database](#2-database)
    - [Operations](#3-operations)
    - [NSUbiquitousKeyValueStore](#4-nsubiquitouskeyvaluestore)
    - [Records](#5-records)


## Integration Tests

#### Running the integration / unit tests on device.
Note: if Unity crashes on startup, you likely have a lingering test scene that is trying to run on startup, just remove them (see below).
1. Make sure your bundle id in Unity (`Project Settings -> Other Settings -> Bundle Identifier`) match the xcode project.
2. Open the Test Runner framework and select `Run All Tests (iOS)`.
3. When the xcode project opens, select your target and then `Signing & Capabilities`.
	1. Add the iCloud capability if missing (`+ Capability`).
	2. Enable the `CloudKit` entitlement and select the `Container` for your project.
	3. Enable the `Key-value storage` entitlement.
4. Run the project.
	1. If you have problems connecting.
		1. Close the app on your device and click allow to the Network popup.
		2. Close XCode.
		3. Remove any lingering Integration test scenes in Unity (InitTestScene#########)
		4. Restart.

## Apple Documentation
#### 1.1 [CloudKit](https://developer.apple.com/documentation/cloudkit)
#### 1.2 [CKDatabase](https://developer.apple.com/documentation/cloudkit/ckdatabase)
#### 1.3 [CKRecord](https://developer.apple.com/documentation/cloudkit/ckrecord)

## Usage

### Exceptions
If there is any error reported from CloudKit, it will be reported via thrown `CloudKitException`. It all cases, you should try/catch to ensure you handle these individual exceptions.
```csharp
private async Task Start()
{
  try
  {
    var result = await ...
  }
  catch(CloudKitException exception)
  {
    Debug.LogError(exception);
  }
}
```
### 1. Container

#### 1.1 Init
```csharp
var container = CKContainer.Init(containerIdentifier);
// The default container is the one that matches iCloud.{productBundleIdentifier}
var defaultcontainer = CKContainer.Default();
```

#### 1.2 Get Account Status
```csharp
var status = await container.GetAccountStatus();
```

#### 1.3 CKCurrentUserDefaultName
You can fetch the const for the CurrentUserDefaultName (for owner of CKRecord):
```csharp
var defaultUserName = CKContainer.CurrentUserDefaultName;
```

### 2. Databases

#### 2.1 Init
You can fetch either the public or private database via the container reference.
```csharp
var privateDatabase = container.PrivateDatabase;
var publicDatabase = container.PublicDatabase;
```

#### 2.2 Save CKRecord
```csharp
var savedRecord = await privateDatabase.Save(record);
```

#### 2.3 Delete Record
```csharp
var deletedRecordId = await privateDatabase.Delete(record);
```

#### 2.4 Fetch Record by CKRecord.ID
```csharp
// Fetches  a record in the privateDatabase by recordName with the default zoneID...
var record = await privateDatabase.Fetch(CKRecord.ID.Init(recordName, CKRecordZone.ID.Default()));
```

### 3. Operations
For more configurable queries, you can use CKDatabaseOperations to handle.

#### 3.1 CKFetchRecordZonesOperation
You can utilize this operation to fetch all zones within a database.

```csharp
var operation = CKFetchRecordZonesOperation.FetchAllRecordZonesOperation();
var recordZones = await privateDatabase.Add(operation);
```

#### 3.2 CKFetchRecordsOperation
You can utilize this operation to fetch a set of records by record ids.

```csharp
var operation = CKFetchRecordsOperation.Init(recordIds);
var records = await privateDatabase.Add(operation);
```

#### 3.3 CKModifyRecordZonesOperation
You can utilize this operation to save or delete record zones within a database.

```csharp
// Save a new recordZone...
var zoneId = CKRecordZone.ID.Init("MyCoolZone");
var recordZone = CKRecordZone.Init(zoneId);
var operation = CKModifyRecordZonesOperation.Init(new [] { recordZone });
result = await privateDatabase.Add(operation);

foreach(var savedZone in result.SavedRecordZones) {
  //...
}

// Delete an existing recordZone...
operation = CKModifyRecordZonesOperation.Init(new [] { zoneId });
result = await privateDatabase.Add(operation);

foreach(var deletedZoneId in result.DeletedRecordZones) {
  //...
}
```

#### 3.4 CKModifyRecordsOperation
```csharp
// Save records...
var operation = CKModifyRecordsOperation.Init(new [] { recordToSave });
operation.SavePolicy = CKModifyRecordsOperation.RecordSavePolicy.ChangedKeys;

var result = await privateDatabase.Add(operation);

foreach(var record in result.SavedRecords) {
  //...
}

// Delete records...
operation = CKModifyRecordsOperation.Init(new [] { recordIdToDelete });
result = privateDatabase.Add(operation);

foreach(var recordId in result.DeletedRecords) {
  //...
}
```

#### 3.4 CKQueryOperation
You can query records and fetch paginated results using the CKQueryOperation. Please see the Apple documentation for NSPredicate and CKQueryOperation.

```csharp
// Fetch all records of 'MyRecordType'...
var query = CKQuery.Init("MyRecordType", NSPredicate.True());

// The initial operation is created with the query reference...
var results = new List<CKRecord>();
var operation = CKQueryOperation.Init(query);

var cursor = await privateDatabase.Add(operation);
results.AddRange(operation.Results);

while(cursor != CKQueryCursor.EmptyCursor) {
  // Subsequent requests, pass in the cursor reference...
  operation = CKQueryOperation.Init(cursor);
  cursor = await privateDatabase.Add(operation);
  results.AddRange(operation.Results);
}

foreach(var record in results) {
  //...
}
```

#### 3.5 CKSubscription
You can setup push notifications about state changes on CloudKit via the CKQuerySubscription, CKDatabaseSubscription, and CKRecordZoneSubscription. Please see the Apple documentation for these classes for more details.

```csharp
// Only call to create once...

var predicate = NSPredicate.Init("tags CONTAINS Swift");
var subscription = CKQuerySubscription.Init("FeedItem", predicate, "tagged-feed-changed", Options.FiresOnReccordCreation);

// Configure the notification so that the system delivers it silently
// and, therefore, doesn't require permission from the user.
var notificationInfo = CKNotificationInfo.Init();
notificationInfo.ShouldSendContentAvailable = true;

subscription.NotificationInfo = notificationInfo;

// Save the subscription to the server...
var operation = CKModifySubscriptionsOperation.Init(new [] { subscription });
operation.Configuration.QualityOfService = QualityOfService.Utility;

try {
    await CKContainer.Default.PrivateDatabase.Add(operation);
}
catch(CloudKitException exception) {
 //...
}
```

### 4 NSUbiquitousKeyValueStore

#### 4.1 Set Properties
Utilize the various Set methods to store values in the user's iCloud container. 

**Please Note:** Each key has a maximum size of 1MB and each user can have up to 1024 keys.
```csharp
NSUbiquitousKeyValueStore.SetInt32("score", 100);
NSUbiquitousKeyValueStore.SetInt64("lastPlayedTimestamp", utcTimestamp);
NSUbiquitousKeyValueStore.SetDouble("currency", 10.4m);
NSUbiquitousKeyValueStore.SetString("username", "CoolKid42");
NSUbiquitousKeyValueStore.SetBool("playedTutorial", true);
NSUbiquitousKeyValueStore.SetBinaryData("characterData", (byte[])data);
```
#### 4.2 Get Properties
Utilize the various Get methods to retrieve the values in the user's iCloud container.

```csharp
var score = NSUbiquitousKeyValueStore.GetInt32("score");
var timestamp = NSUbiquitousKeyValueStore.GetInt64("lastPlayedTimestamp");
var currency = NSUbiquitousKeyValueStore.GetDouble("currency");
var username = NSUbiquitousKeyValueStore.GetString("username");
var playedTutorial = NSUbiquitousKeyValueStore.GetBool("playedTutorial");
var data = NSUbiquitousKeyValueStore.GetBinaryData("characterData");
```

#### 4.3 DidChangeExternallyNotification
Register for the callbacks for when values are changed externally like so:

```csharp
NSUbiquitousKeyValueStore.DidChangeExternally += OnDidChangeExternally;

private void OnDidChangeExternally(object sender, DidChangeExternallyEventArgs args)
{
    switch(args.Response.NSUbiquitousKeyValueStoreChangeReasonKey)
    {
        case DidChangeExternallyReason.ServerChange:
        break;
        case DidChangeExternallyReason.InitialSyncChange:
        break;
        case DidChangeExternallyReason.QuotaViolationChange:
        break;
        case DidChangeExternallyReason.AccountChange:
        break;
    }
}
```

### 5 CKRecord

#### 5.1 Set Properties
Utilize the various Set methods to store values in the user's iCloud container. 

```csharp
record.Set("score", 100);
record.Set("lastPlayedTimestamp", utcTimestamp);
record.Set("username", "CoolKid42");
record.Set("playedTutorial", true);
record.Set("characterData", (byte[])data);
```

#### 5.2 Get Properties
Utilize the various Get methods to retrieve the values in the user's iCloud container.

```csharp
var score = record.GetInt32("score");
var timestamp = record.GetInt64("lastPlayedTimestamp");
var username = record.GetString("username");
var playedTutorial = record.GetBool("playedTutorial");
var data = record.GetBinaryData("characterData");
```

#### 5.3 Get Attributes
```csharp
var hasUsername = record.HasKey("username");
var timestamp = record.CreationDate;
var timestamp = record.LastModificationDate;
var creatorId = record.CreatorUserRecordID;
var userId = record.LastModifiedUserRecordID;
var parentRecord = record.GetParent(recordId);
var type = record.RecordType;
var tag = record.RecordChangeTag;
```

