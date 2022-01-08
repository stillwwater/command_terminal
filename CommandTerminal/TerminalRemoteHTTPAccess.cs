using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Text;

//This is a simple http listener that sends strings as commands to the terminal shell
//It receives 2 keys, password and command. Checks if password is correct before sending command string.
//TODO: parametrize ports and password from gamedata.serversettings
public class TerminalRemoteHTTPAccess : MonoBehaviour
{

	private HttpListener listener;
	private Thread listenerThread;
	public string password = "password";
	private bool passwordCorrect = false;

	void Start ()
	{
		listener = new HttpListener ();
		listener.Prefixes.Add ("http://localhost:4444/");
		listener.Prefixes.Add ("http://127.0.0.1:4444/");
		listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
		listener.Start ();

		listenerThread = new Thread (startListener);
		listenerThread.Start ();
		Debug.Log ("Remote Terminal HTTP Listener Server Started");
	}

	private void startListener ()
	{
		while (true) {               
			var result = listener.BeginGetContext (ListenerCallback, listener);
			result.AsyncWaitHandle.WaitOne ();
		}
	}

	private void ListenerCallback (IAsyncResult result)
	{				
		var context = listener.EndGetContext (result);		
		// Debug.Log ("Method: " + context.Request.HttpMethod);
		// Debug.Log ("LocalUrl: " + context.Request.Url.LocalPath);
		passwordCorrect = false;
		if (context.Request.QueryString.AllKeys.Length > 0)
			foreach (var key in context.Request.QueryString.AllKeys) {
				// Debug.Log ("Key: " + key + ", Value: " + context.Request.QueryString.GetValues (key) [0]);
                if (key == "password") {
                    if (password == context.Request.QueryString.GetValues(key)[0]) {
						passwordCorrect = true;
					} else {
						Response(context, "Password incorrect.");
					}
                }
			}

		if (context.Request.QueryString.AllKeys.Length > 0 && passwordCorrect)
			foreach (var key in context.Request.QueryString.AllKeys) {
				// Debug.Log ("Key: " + key + ", Value: " + context.Request.QueryString.GetValues (key) [0]);
                if (key == "command") {
					CommandTerminal.Terminal.Shell.RunCommand(context.Request.QueryString.GetValues(key)[0]);
					Response(context, "Command executed.");
                }
			}


		context.Response.Close ();
	}

	public void Response(HttpListenerContext context, string message) {
		string logContent = message; // your json string here
		context.Response.ContentType = "application/json";
		byte[] encodedPayload = new UTF8Encoding().GetBytes(logContent);
		context.Response.ContentLength64 = encodedPayload.Length;
		System.IO.Stream output = context.Response.OutputStream;
		output.Write(encodedPayload, 0, encodedPayload.Length);
	}
}
