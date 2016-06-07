﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ILyncH
{
    public enum NotificationLevel
    {
        Debug,
        Message,
        Error,
        Fatal,
    }

    public interface INotify
    {
        void Connected();

        void NotConnect();

        void OCNotRuning();

        void OCRunning();
    }
}
