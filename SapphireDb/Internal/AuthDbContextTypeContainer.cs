﻿using System;

namespace SapphireDb.Internal
{
    public class AuthDbContextTypeContainer
    {
        public Type DbContextType { get; set; }

        public Type UserType { get; set; }

        public Type UserManagerType { get; set; }
    }
}