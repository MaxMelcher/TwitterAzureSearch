# TwitterAzureSearch
Demo app to show off Azure Search combined with twitter and Azure ML

Scenario: 
The App pulls in tweets and stores them into a SharePoint List. 


**The missing app.config**
The app config contains my keys to Azure Search, Twitter, Azure ML - so they are not in there. Please get your own keys and add them to the app.config file like this:

  <?xml version="1.0" encoding="utf-8"?>
  <configuration>
    <startup>
      <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5" />
    </startup>
    <appSettings>
    
      <!-- azure search-->
      <add key="SearchServiceName" value="XXX" />
      <add key="SearchServiceApiKey" value="XXX" />
      
      <!-- twitter -->
      <add key="consumerKey" value="XXX" />
      <add key="consumerSecret" value="XXX" />
      <add key="accessToken" value="XXX" />
      <add key="accessTokenSecret" value="XXX" />
      
      <!-- Azure ML -->
      <add key="accountKey" value="XXX" />
    </appSettings>
    <runtime>
      <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
        <dependentAssembly>
          <assemblyIdentity name="System.Net.Http.Primitives" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
          <bindingRedirect oldVersion="0.0.0.0-4.2.28.0" newVersion="4.2.28.0" />
        </dependentAssembly>
      </assemblyBinding>
    </runtime>
  </configuration>

