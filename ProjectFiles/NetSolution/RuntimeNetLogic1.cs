#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.NetLogic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using FTOptix.System;
using FTOptix.EventLogger;
using FTOptix.Store;
using FTOptix.SQLiteStore;
#endregion

public class RuntimeNetLogic1 : BaseNetLogic
{
    private Device device;
    private string username;
    private string actualPassword;
    private string deviceIP;
    private string newPassword;

    public override void Start()
    {
        deviceIP = LogicObject.GetVariable("deviceIP").Value;
        newPassword = LogicObject.GetVariable("newPassword").Value;
        actualPassword = LogicObject.GetVariable("actualPassword").Value;
        device = new Device(deviceIP);
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void DeviceUpdatePassword()
    {
        LongRunningTask longRunningTask = new LongRunningTask(DeviceLoginAdUpdatePassword, LogicObject);
        longRunningTask.Start();
    }

    [ExportMethod]
    public void DeviceImportConfiguration()
    {

    }

    private async void DeviceLoginAdUpdatePassword()
    {
        var loginRes = await device.Login(username, actualPassword);
        if (loginRes.IsSuccessStatusCode)
        {
            var changePswRes = await device.ChangePassword(username, newPassword);
            if (changePswRes.IsSuccessStatusCode)
            {
                Log.Info("Password changed correctly");
            }
            else
            {
                Log.Error("Device change password error: " + changePswRes.StatusCode);
                return;
            }
        }
        else
        {
            Log.Error("Device login error: " + loginRes.StatusCode);
            return;
        }
    }
}

public class Device
{
    private readonly HttpClient _client;
    private string _reachableIp;
    private string _password;

    public Device(string reachableIp)
    {
        _reachableIp = reachableIp;
        _client = new HttpClient { BaseAddress = new Uri($"https://{reachableIp}/") };
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<HttpResponseMessage> Login(string username, string password)
    {
        var loginData = new { username, password };
        var content = new StringContent(JsonSerializer.Serialize(loginData), Encoding.UTF8, "application/json");
        return await _client.PostAsync("/login", content);
    }

    public async Task<HttpResponseMessage> RetrieveConfigurationsList()
    {
        return await _client.GetAsync("/api/deviceconfiguration/export");
    }

    public async Task<HttpResponseMessage> ExportConfigurations(List<object> jsonConfigurationsList)
    {
        var content = new StringContent(JsonSerializer.Serialize(jsonConfigurationsList), Encoding.UTF8, "application/json");
        return await _client.PostAsync("/api/deviceconfiguration/export?location=web", content);
    }

    public async Task<HttpResponseMessage> ImportConfigurations(object jsonExportedConfigurations)
    {
        var content = new StringContent(JsonSerializer.Serialize(jsonExportedConfigurations), Encoding.UTF8, "application/json");
        return await _client.PatchAsync("/api/deviceconfiguration/import?location=web", content);
    }

    public async Task<HttpResponseMessage> ChangePassword(string username, string newPassword)
    {
        var payload = new { username, password = newPassword };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _client.PatchAsync("/api/users/accounts", content);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            _password = newPassword; // Assuming password storage is appropriate
        }

        return response;
    }
}

