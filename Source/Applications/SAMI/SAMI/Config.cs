//******************************************************************************************************
//  Config.cs - Gbtc
//
//  Copyright © 2020, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  09/10/2020 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using GSF.Net.Snmp;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using Ciloci.Flee;
using GSF;
using GSF.Diagnostics;

namespace SAMI
{
    public enum EventType
    {
        Success = 1,
        Warning,
        Alarm,
        Error,
        Information,
        Escalation,
        Failover,
        Quit,
        Synchronize,
        Reschedule,
        CatchUp
    }

    public class Mapping
    {
        public const string DefaultImports = "AssemblyName=mscorlib, TypeName=System.Math; AssemblyName=mscorlib, TypeName=System.DateTime";

        private IDynamicExpression m_expression;

        public Mapping()
        {
            foreach (string typeDef in DefaultImports.Split(';'))
            {
                try
                {
                    Dictionary<string, string> parsedTypeDef = typeDef.ParseKeyValuePairs(',');
                    string assemblyName = parsedTypeDef["assemblyName"];
                    string typeName = parsedTypeDef["typeName"];
                    Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
                    Type type = assembly.GetType(typeName);

                    ExpressionContext.Imports.AddType(type);
                }
                catch (Exception ex)
                {
                    string message = $"Unable to load type from assembly: {typeDef}";
                    Logger.SwallowException(new ArgumentException(message, ex));
                }
            }
        }

        [XmlAttribute("oid")]
        public string OID { get; set; }

        [XmlAttribute("flow")]
        public string Flow { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; } = "{Value} at {Timestamp}";

        [XmlAttribute("state")]
        public string State { get; set; } = "Success";

        [XmlAttribute("condition")]
        public string Condition { get; set; } = "true";

        public void SetValue(object value) => ExpressionContext.Variables["value"] = value;

        [XmlIgnore]
        public bool ConditionSuccessful => Expression.Evaluate().ToString().ParseBoolean();

        [XmlIgnore]
        public EventType EventType { get; set; } = EventType.Success;

        [XmlIgnore]
        public ExpressionContext ExpressionContext { get; set; } = new ExpressionContext();

        [XmlIgnore]
        public IDynamicExpression Expression => m_expression ?? (m_expression = ExpressionContext.CompileDynamic(Condition));
    }

    public class Source
    {
        [XmlAttribute("community")]
        public string Community { get; set; }

        [XmlAttribute("authPhrase")]
        public string AuthPhrase { get; set; }
        
        [XmlAttribute("encryptKey")]
        public string EncryptKey { get; set; }

        [XmlElement(ElementName = "mapping")]
        public List<Mapping> Mappings { get; set; } = new List<Mapping>();

        [XmlIgnore]
        public Dictionary<ObjectIdentifier, Mapping> OIDMap = new Dictionary<ObjectIdentifier, Mapping>();

        public void ParseMappings()
        {
            foreach (Mapping mapping in Mappings)
            {
                if (!Enum.TryParse<EventType>(mapping.State, out EventType eventType))
                    eventType = EventType.Success;

                mapping.EventType = eventType;

                OIDMap[new ObjectIdentifier(mapping.OID)] = mapping;
            }
        }
    }

    [XmlRoot(ElementName = "config")]
    public class Config
    {
        [XmlElement(ElementName = "source")]
        public List<Source> Sources { get; set; } = new List<Source>();

        [XmlIgnore]
        public Dictionary<string, Source> CommunityMap = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);

        public static Config Load(string filepath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Config));

            using (StreamReader reader = new StreamReader(filepath))
            {
                Config config = (Config)serializer.Deserialize(reader);
                
                foreach (Source source in config.Sources)
                    config.CommunityMap[source.Community] = source;

                return config;
            }
        }

        public static void Save(Config config, string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Config));

            using (StreamWriter writer = new StreamWriter(filePath))
                serializer.Serialize(writer, config);
        }
    }
}
