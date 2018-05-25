using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BeatSaberMultiplayer
{
    
    public static class ReflectionUtil
    {
        public static void SetPrivateField(object obj, string fieldName, object value)
        {
            obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(obj, value);
        }
        
        public static T GetPrivateField<T>(object obj, string fieldName)
        {
            return (T)((object)obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj));
        }
        
        public static void SetPrivateProperty(object obj, string propertyName, object value)
        {
            obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(obj, value, null);
        }
        
        public static void InvokePrivateMethod(object obj, string methodName, object[] methodParams)
        {
            obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, methodParams);
        }
    }

}
