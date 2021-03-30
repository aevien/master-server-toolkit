using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace MasterServerToolkit.MasterServer.Web
{
    public static class XmlElementExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        public static void SetId(this XmlElement el, string id)
        {
            el.SetAttribute("id", id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el"></param>
        /// <param name="className"></param>
        public static void AddClass(this XmlElement el, string className)
        {
            if (el.HasAttribute("class"))
            {
                List<string> classes = el.GetAttribute("class").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                if (!classes.ToList().Contains(className.Trim()))
                {
                    classes.Add(className);
                }

                el.SetAttribute("class", string.Join(" ", classes));
            }
            else
            {
                el.SetAttribute("class", className);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el"></param>
        /// <param name="className"></param>
        public static void RemoveClass(this XmlElement el, string className)
        {
            if (el.HasAttribute("class"))
            {
                List<string> classes = el.GetAttribute("class").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                if (!classes.ToList().Contains(className.Trim()))
                {
                    classes.Remove(className);
                }

                el.SetAttribute("class", string.Join(" ", classes));
            }
        }
    }
}