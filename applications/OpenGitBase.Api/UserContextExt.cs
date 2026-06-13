using System;
using OpenGitBase.Common.Auth;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api
{
    public static class UserContextExt
    {
        public static UserId GetUserId(this IUserContext userContext)
        {
            if (userContext == null)
            {
                throw new ArgumentNullException(nameof(userContext));
            }

            return UserId.From(userContext.User.UserId);
        }
    }
}
