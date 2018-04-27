# Build a serverless intelligent license plate recognition system

In this lab we will walk through building a serverless license plate processing system.  The problem statement is fairly simple, and was inspired by a real Azure use case.  A city has roads that charge a toll, and in order to enforce that toll they are taking pictures of the license plates of each car as it passes through.  Today the system has a few problems:

* It's all running on dedicated infrastructure (servers) which have capacity and availability issues
* All images are sent to a vendor company to identify (often manually) the numbers
* The solution is so tightly coupled its difficult to make changes without introducing disruption

We want to go through and modernize this system to work in a serverless world.

## What we will build

The eventual solution we are building will have the following flow:

1. A license plate image is taken from a toll booth and uploaded to an Azure storage account
2. Azure Event Grid will send an instant notification to a serverless function
3. An Azure Function will take the plate data, and process it using a cognitive service to attempt to read the text
4. If the text is unreadable, another event will fire to start a manual review process (Azure Logic Apps)

## Your lab environment

To start, login to the lab. You should see these instructions on the right hand side.  If you click the **Resources** tab you can also see things like a login to an Azure environment.  

# Writing the Azure Function

Let's start with writing the Azure Function that will process the license plate images.  Azure Functions provides serverless compute - this means our code will run in response to images being uploaded, and will scale automatically to the load coming in.  The city in this case will only have to pay for the executions that occur, so if no one is driving on the road, no functions would be fired or paid for.  On the other hand if a large event happened and hundreds of cars are getting their plate images taken, the app would automatically scale to process all events.

## Create the function

1. On the taskbar at the bottom of the screen, open the **Visual Studio 2017** program.
    ![VS icon][1_VS]
1. Click the **File** menu and select **New** -> **Project**
1. Create a new Azure Functions project (under **Visual C#** -> **Cloud**). It should automatically be created. Feel free to rename to whatever you want.
1. Select to start with an **Empty** project template and leave the default **Storage Emulator** for development.  Select **Ok**
    ![Project template][2]
1. In the **Solution Explorer** on the right-hand side you should see the function project.
    * The `local.settings.json` file is for storing and creating environment variables and connection strings to be used in the function.
    * The `host.json` file is for some advanced configuration on the host (things like batch size and concurrency controls)
1. Right-click the project file and select **Add** -> **New item** (or new Azure Function)
    ![Add Azure Function][3]
1. Select **Azure Function**, leave as `Function1.cs`, and press **Add**
1. Select the **Event Grid trigger** and press **Ok**
    > Note: The "Blob trigger" could also work here. There are some differences in how the two triggers work today. Most notably the current "Blob trigger" works by 'polling' the storage account for changes and can only trigger on one storage account per function. Event Grid sends an instant event notification, and the same function could process many storage account events.  In future these two triggers should merge.

## Authoring the Event Grid function

Visual Studio show now be showing a function for Event Grid.  You can see the trigger will be an *EventGridEvent*, and right now the code will just log the data from the event.

1. Replace the code with the following [here](code_snippets/eventGridFunction.cs)

If interested in the code it's commented throughout the way. In short it's doing the following:

* Trigger on an event grid message
* `Stream plate` is an *input binding* that will automatically go and pull the image bytes in from storage with the event
* `outBlob` is out *output binding* that will write a `result.txt` file in the same storage account with results of the trigger
* Run the `plate` stream through cognitive services to see if it can pull text
* If text is identified, write the result to `result.txt` file
* If no text is identified, send an event to event grid for manual processing

## Debugging the Azure Function locally

Before testing this Azure Function locally we need to provide a few things - namely the keys and environment values for your account.

### Setting environment variables

1. In your solution explorer, open the `local.settings.json` file to configure environment variables.  We have 4 that are defined in the code pasted into our function:
    |Name|Description|
    |--|--|
    |eventGridUrl|Url for the Event Grid topic we SEND for manual review|
    |eventGridKey|Key for the Event Grid topic we SEND for manual review|
    |visionApiUrl|Url for the computer vision resource|
    |visionApiKey|Key for the computer vision resource|

1. Overwrite the `local.settings.json` to this format (we'll fill in the values here shortly):

    ```json
    {
        "IsEncrypted": false,
        "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "AzureWebJobsDashboard": "UseDevelopmentStorage=true",
        "eventGridUrl": "",
        "eventGridKey": "",
        "visionApiUrl": "",
        "visionApiKey": ""
        }
    }
    ```

1. Open the [Azure Portal](https://portal.azure.com) and enter in the username and password provided in the resources to this lab.
1. In the **All Resources** tile, select the **event-grid-topic** so we can get its URL and key
    ![Portal dash][4]
1. Copy the **Topic Endpoint** near the top middle of the screen, and paste it in the `eventGridUrl` value in the `local.settings.json` file of Visual Studio
    ![Topic Endpoint][5]
1. In the browser select the **Access Keys** setting. Copy the value of **Key 1** and paste in the `eventGridKey` value for `local.settings.json`
1. Go back to the Azure Dashboard (can get to quickly by pushing the **Microsoft Azure** icon in the top left), and open the **computervision** resource.
    ![Computer Vision][6]
1. On the left-hand side click the **Keys** section and copy **Key 1** and paste in the `visionApiKey` value in `local.settings.json`
1. On the left-hand side click the **Overview** section and copy the **Endpoint** from the essentials menu. Paste this endpoint in the `visionApiUrl` value.
    ![Endpoint][7]
1. Your `local.settings.json` file should not have values for every property.

### Running the function runtime

Now that the environment is configured against your subscription and keys, we can run and test the function.

1. Open the **Function1.cs** file in your solution (the Azure Function code)
1. Click the run button at the top of Visual Studio

You should see the Azure Functions runtime eventually pop up and start running in a console. This is the same runtime that will execute when published to the cloud, but here now running on your machine to debug the app.

Now we need to simulate an Event Grid message from a plate getting added to Azure Storage.  Since we are working on the local environment we are set to use the Azure Storage Emulator.  Some images have already been added to the storage emulator (Plate1.jpg, Plate2.jpg, Unreadable.jpg).  You can see those in the Azure Storage Explorer (on task bar) or on desktop if curious.

To debug and test Event Grid messages locally:

1. Open up **Postman** (on taskbar) to send an HTTP request
    ![Postman][8]
1. A request called **Local debug** should already be there for you.  It's making a `POST` request, to your localhost where the Azure Function is running.  The URL is set to send an event to the function called `Analyze` and has the request body of an event grid message.
1. Press the **Send** button to send the event
1. You can switch back over to the Function runtime window and see the log messages get generated. You should see it ran and pulled out the license plate number
1. Feel free to re-run the test with other files by changing the URL property to end with one of the 3 other names (`plate1.jpg`, `plate2.jpg`, or `unreadable.jpg`). **It is case sensitive**
    ![url][9]

You can see the files in your storage emulator, and the results that are generated by opening the **Azure Storage Explorer** on the taskbar and opening **(Local and Attached)** -> **Storage Accounts** -> **Blob Containers** -> **plates**
    ![storage explorer][10]

### Publishing the Azure Function

Now that the code is working locally, we can publish to the cloud.

1. Right click the project and select **Publish**
    ![publish][11]
1. Select **Select Existing** and press **Publish**
1. In the account dropdown select **Add an account**
    ![addaccount][12]
1. Enter the username and password in the Resources tab for your Azure account for this lab.
1. Select your pre-provisioned resource group and pre-provisioned function app and press **Ok**
    ![selectfunction][13]
1. A publish should automatically start (you can see the details in the output window). This will take about a minute.
1. Click "Yes" to update your Azure Function to latest `beta` version

The Azure Function is now published to Azure.

1. Open the Azure Portal and login with your provided account
1. Open your Azure Function (should be a lightning icon and end with "func")
1. Your environment variables aren't yet set, so you need to add them here. Go ahead and click the **Application settings** link on your function app.
    ![appsettings][14]
1. Add app settings for the 4 new values in your `local.settings.json` file in Visual Studio.  In the end it should look like this
    ![appsettings][15]
1. Scroll to the top and click **Save** to save the environment variables your code needs.

The function is now published and configured in the cloud environment. You could also hook up a CI/CD system like Visual Studio Team Services to automatically deploy and configure your environment, but we kept manual for now.

The last step is to hook up this Azure Function to the event sources we care about. For us that's the Azure storage account in the cloud that the license plate images will land in.

1. Click the **Analyze** function in your function app.  This is the one we just published from Visual Studio.
    ![analyze][16]
1. Click the **Add Event Grid subscription** link to create an event grid subscription.
    ![sub][17]
1. Fill in with the following:
    |Field|Value|
    |--|--|
    |Name|AnalyzeFunction|
    |Topic Type|Storage Accounts|
    |Subscription|{leave as is}|
    |Resource group|{leave as is}|
    |Instance|Select the only item|
    |Event Types|**Deselect** all, and choose only **Blob Created** events|
    |Subscriber Type|{leave as is}|
    |Subscriber Endpoint|{leave as is}|
    |Prefix Filter|{leave as is}|
    |Suffix Filter|.jpg|
1. Click create to have all `.jpg` files from your actual storage account fire a `BlobCreated` event to this Azure Function.

## Testing in Azure

The last step here before the extra credit is to run one last test of your now published function.  To do that we need to add files to your storage account.

1. Go back to the Azure Dashboard (click the **Microsoft Azure** logo in the top left)
1. Open your storage account (green square icon, ends with `stor`)
1. Click the **Open in Storage Explorer** icon
    ![][18]
1. Select **Yes** to download, **Yes** to switch apps, and **Select** to select subscriptions.  Click **Apply** and it should bring you to your subscription and storage account in storage explorer.
1. Expand to **Blob Containers** and right-click to **Create Blob Container**
    ![][19]
1. Name the container whatever you want (`plates`), and then click the **Upload** button on the toolbar, select **Upload files** and select some or all of the images in the **Desktop** folder called **Plates**.
1. Refresh (on the toolbar, may be under **... More**) and after a few seconds you should see the `-result.txt` files generate.  You can open them and it should have the license plate number or result of the runs.

Great Job! You now have a serverless app that can dynamically scale for any plate images added to this storage account.

# Extra Credit - create the logic app

The last piece we can create is the workflow to process unidentified license plates. For that we will use serverless logic apps to kick off a workflow.

1. Go back into the Azure Portal and the dashboard and select `logicapp-build-ill` Logic App.
1. In the designer (or click **Edit** if designer doesn't open) select the **When a Event Grid event occurs** common trigger template
1. Click **Sign in** to authenticate this workflow trigger with your Azure Subscription. Click **Sign in** again to keep the default directory tenant.
1. Click **Continue** to open the designer
1. Select your subscription from the **Subscription** dropdown.
1. Select `Microsoft.EventGrid.Topics` from the Resource Type (these are events we are sending to our own custom topic in our function code).
1. Select the `event-grid-topic` in the Resource Name

Now that we've configured this workflow to fire on custom events, let's add some steps.  Let's have it send an email that an image needs to be reviewed.

1. Click **New Step** and **Add an Action**
1. Select or search for a connector to send an email with an account you have (Office 365 Outlook, Outlook.com, Gmail).  There may be an Office 365 Outlook account for you in the resources tab of this lab.
1. Sign in to authenticate the workflow to send emails.
1. Set the to address, subject, and body.  For the body you can write something like: "An image needs to be manually reviewed: [Subject]" and pass in the subject from the Event Grid 
    ![20][20]

1. Click **Save**

If you wanted you could continue to build out more logic here, and in the real world use case this workflow involved human interaction and approvals, but for the sake of simplicity we'll leave here.

## Testing the Logic App

Now that the logic app is setup to send an email notifying that a plate needs manual review, let's test it out.

1. Open the storage explorer from before.
1. Add or rename the `unreadable.jpg` file.  This should fire the `BlobCreated` event, hit the Function, send an event, and fire the logic app.
1. Check your mailbox - you should have the mailbox.

<!-- images -->
[1_VS]: images/vs.png
[2]: images/2.png
[3]: images/3_newitem.png
[4]: images/4_eventgrid.png
[5]: images/5_topicurl.png
[6]: images/6_computervision.png
[7]: images/7_endpoint.png
[8]: images/8_postman.png
[9]: images/9_plate2.png
[10]: images/10_storageexplorer.png
[11]: images/11_publish.png
[12]: images/12_addaccount.png
[13]: images/13_selectfunction.png
[14]: images/14_appsettings.png
[15]: images/15_appsettings2.png
[16]: images/16_analyze.png
[17]: images/17_sub.png
[18]: images/openinexplorer.png
[19]: images/19_createcontainer.png
[20]: images/20.png