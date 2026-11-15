using System;
using System.Reflection;
using HarmonyLib;

namespace KaenoAdaptiveTraderUI.Patches
{
    internal static class PatchTools
    {
        /// <summary>
        /// 只在声明类型自身的方法里按名字找（DeclaredOnly），避免基类同名重载导致 AmbiguousMatch。
        /// SPT/EFT 的方法签名在小版本间可能变化，所以优先按“声明类型 + 方法名（+参数个数）”定位。
        /// </summary>
        internal static MethodBase FindDeclaredMethod(Type type, string name, int parameterCount = -1)
        {
            if (type == null)
            {
                return null;
            }

            MethodInfo[] methods;
            try
            {
                methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                                          | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            }
            catch (Exception)
            {
                return null;
            }

            MethodInfo fallback = null;
            foreach (var method in methods)
            {
                if (method == null || method.Name != name)
                {
                    continue;
                }

                if (parameterCount < 0 || method.GetParameters().Length == parameterCount)
                {
                    if (fallback == null)
                    {
                        fallback = method;
                    }
                }
            }

            if (fallback != null)
            {
                return fallback;
            }

            // 极端回退：用 Harmony 的 AccessTools（可能抛异常，由调用方 catch）
            try
            {
                return AccessTools.Method(type, name);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
