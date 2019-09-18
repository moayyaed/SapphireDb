﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RealtimeDatabase.Models;
using RealtimeDatabase.Models.Responses;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using RealtimeDatabase.Helper;
using RealtimeDatabase.Internal;
using RealtimeDatabase.Models.Commands;

namespace RealtimeDatabase.Connection.Websocket
{
    class WebsocketMiddleware
    {
        private readonly RealtimeConnectionManager connectionManager;
        private readonly RealtimeDatabaseOptions options;

        // ReSharper disable once UnusedParameter.Local
        public WebsocketMiddleware(RequestDelegate next, RealtimeConnectionManager connectionManager, RealtimeDatabaseOptions options)
        {
            this.connectionManager = connectionManager;
            this.options = options;
        }

        public async Task Invoke(HttpContext context, CommandExecutor commandExecutor, IServiceProvider serviceProvider, ILogger<WebsocketConnection> logger)
        {
            if (context.WebSockets.IsWebSocketRequest && await CheckAuthentication(context))
            {
                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                WebsocketConnection connection = new WebsocketConnection(webSocket, context);

                connectionManager.AddConnection(connection);
                await connection.Send(new ConnectionResponse() {
                    ConnectionId = connection.Id,
                    BearerValid = context.User.Identity.IsAuthenticated
                });

                while (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
                {
                    try
                    {
                        string message = await connection.Websocket.Receive();

                        if (!string.IsNullOrEmpty(message))
                        {
                            _ = Task.Run(async () =>
                            {
                                CommandBase command = JsonHelper.DeserializeCommand(message);

                                if (command != null)
                                {
                                    ResponseBase response = await commandExecutor.ExecuteCommand(command,
                                        serviceProvider.CreateScope().ServiceProvider, connection.HttpContext, logger, connection);

                                    if (response != null)
                                    {
                                        await connection.Send(response);
                                    }
                                }
                            });
                        }
                    }
                    catch(Exception ex)
                    {
                        logger.LogError(ex.Message);
                    }
                }

                connectionManager.RemoveConnection(connection);
            }
        }

        private async Task<bool> CheckAuthentication(HttpContext context)
        {
            if (!string.IsNullOrEmpty(options.Secret))
            {
                if (context.Request.Query["secret"] != options.Secret)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("The secret does not match");
                    return false;
                }
            }

            return true;
        }
    }
}
