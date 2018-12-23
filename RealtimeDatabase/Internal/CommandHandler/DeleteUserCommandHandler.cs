﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using RealtimeDatabase.Models.Auth;
using RealtimeDatabase.Models.Commands;
using RealtimeDatabase.Models.Responses;
using RealtimeDatabase.Websocket.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeDatabase.Internal.CommandHandler
{
    class DeleteUserCommandHandler : AuthCommandHandlerBase, ICommandHandler<DeleteUserCommand>
    {
        private readonly AuthDbContextTypeContainer contextTypeContainer;

        public DeleteUserCommandHandler(AuthDbContextAccesor authDbContextAccesor, AuthDbContextTypeContainer contextTypeContainer, IServiceProvider serviceProvider)
            : base(authDbContextAccesor, serviceProvider)
        {
            this.contextTypeContainer = contextTypeContainer;
        }

        public async Task Handle(WebsocketConnection websocketConnection, DeleteUserCommand command)
        {
            dynamic usermanager = serviceProvider.GetService(contextTypeContainer.UserManagerType);

            IdentityUser user = await usermanager.FindByIdAsync(command.Id);

            if (user != null)
            {
                IdentityResult result = 
                    await(dynamic)contextTypeContainer.UserManagerType.GetMethod("DeleteAsync").Invoke(usermanager, new object[] { user });

                if (result.Succeeded)
                {
                    IRealtimeAuthContext db = GetContext();

                    db.UserRoles.RemoveRange(db.UserRoles.Where(ur => ur.UserId == user.Id));
                    db.SaveChanges();

                    await SendMessage(websocketConnection, new DeleteUserResponse()
                    {
                        ReferenceId = command.ReferenceId
                    });
                    return;
                }
            }

            await SendMessage(websocketConnection, new DeleteUserResponse()
            {
                ReferenceId = command.ReferenceId,
                Error = new Exception("Deleting user failed")
            });
        }
    }
}