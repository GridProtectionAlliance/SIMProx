//******************************************************************************************************
//  SnmpHost.cs - Gbtc
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
//  01/21/2020 - Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

// TODO: An even more generic version of this adapter should be added to GSF extending DynamicCalculator

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using GSF;
using GSF.Diagnostics;
using GSF.Threading;
using GSF.TimeSeries;
using GSF.TimeSeries.Adapters;
using ConnectionStringParser = GSF.Configuration.ConnectionStringParser<GSF.TimeSeries.Adapters.ConnectionStringParameterAttribute>;

// ReSharper disable PossibleMultipleEnumeration
namespace SAMI
{
    /// <summary>
    /// Represents an event triggering adapter.
    /// </summary>
    [Description("Event Trigger: Triggers an openMIC \"QueueTasks\" operation on point status change.")]
    public class SnmpHost : FacileActionAdapterBase
    {
        #region [ Members ]

        // Constants

        /// <summary>
        /// Defines the default value for the <see cref="TriggerValue"/>.
        /// </summary>
        public const double DefaultTriggerValue = 0.0D;

        /// <summary>
        /// Defines the default value for the <see cref="TriggerFromInitialValue"/>.
        /// </summary>
        public const bool DefaultTriggerFromInitialValue = true;

        /// <summary>
        /// Defines the default value for the <see cref="TriggerAction"/>.
        /// </summary>
        public const string DefaultTriggerAction = "http://localhost:8089/api/Operations/QueueTasks?taskID=_AllTasksGroup_&priority=Expedited&target=Meter1&target=Meter2&target=Meter3";

        /// <summary>
        /// Defines the default value for the <see cref="TriggerActionUserName"/>.
        /// </summary>
        public const string DefaultTriggerActionUserName = "$env:openMICTriggerUserName";

        /// <summary>
        /// Defines the default value for the <see cref="TriggerActionPassword"/>.
        /// </summary>
        public const string DefaultTriggerActionPassword = "$env:openMICTriggerPassword";

        /// <summary>
        /// Token used to read credential from environmental variable.
        /// </summary>
        public const string EnvPrefixToken = "$env:";

        // Fields
        private ShortSynchronizedOperation m_triggerOperation;
        private bool m_initialValueReceived;

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets point tag to monitor for triggering.
        /// </summary>
        [ConnectionStringParameter]
        [Description("Defines the point tag to monitor for triggering.")]
        public string TriggerPointTag { get; set; }

        /// <summary>
        /// Gets or sets value that will invoke the trigger.
        /// </summary>
        [ConnectionStringParameter]
        [Description("Defines the value that will invoke the trigger.")]
        [DefaultValue(DefaultTriggerValue)]
        public double TriggerValue { get; set; } = DefaultTriggerValue;

        /// <summary>
        /// Gets or sets value that determines if the trigger will fire on startup if the initial value is set to the trigger value.
        /// </summary>
        [ConnectionStringParameter]
        [Description("Defines the value that determines if the trigger will fire on startup if the initial value is set to the trigger value.")]
        [DefaultValue(DefaultTriggerFromInitialValue)]
        public bool TriggerFromInitialValue { get; set; } = DefaultTriggerFromInitialValue;

        /// <summary>
        /// Gets or sets the HTTP GET method trigger action to invoke.
        /// </summary>
        [ConnectionStringParameter]
        [Description("Defines the HTTP GET method trigger action to invoke.")]
        [DefaultValue(DefaultTriggerAction)]
        public string TriggerAction { get; set; } = DefaultTriggerAction;

        /// <summary>
        /// Gets or sets the HTTP GET method trigger action username. Use "$env:" prefix to pull from environmental variable.
        /// </summary>
        [ConnectionStringParameter]
        [Description("Defines the HTTP GET method trigger action username. Use \"" + EnvPrefixToken + "\" prefix to pull from environmental variable.")]
        [DefaultValue(DefaultTriggerActionUserName)]
        public string TriggerActionUserName { get; set; } = DefaultTriggerActionUserName;

        /// <summary>
        /// Gets or sets the HTTP GET method trigger action password. Use "$env:" prefix to pull from environmental variable.
        /// </summary>
        [ConnectionStringParameter]
        [Description("Defines the HTTP GET method trigger action password. Use \"" + EnvPrefixToken + "\" prefix to pull from environmental variable.")]
        [DefaultValue(DefaultTriggerActionPassword)]
        public string TriggerActionPassword { get; set; } = DefaultTriggerActionPassword;

        /// <summary>
        /// Gets the flag indicating if this adapter supports temporal processing.
        /// </summary>
        public override bool SupportsTemporalProcessing => false;

        /// <summary>
        /// Returns the detailed status of the data input source.
        /// </summary>
        public override string Status
        {
            get
            {
                StringBuilder status = new StringBuilder();

                status.Append(base.Status);
                status.AppendFormat("         Trigger Point Tag: {0}", TriggerPointTag);
                status.AppendLine();
                status.AppendFormat("            Trigger Action: {0}", TriggerAction);
                status.AppendLine();
                status.AppendFormat("  Trigger Action User Name: {0}", DeriveCredential(TriggerActionUserName));
                status.AppendLine();
                status.AppendFormat("   Trigger Action Password: {0}", string.IsNullOrWhiteSpace(DeriveCredential(TriggerActionPassword)) ? "Undefined" : "Defined");
                status.AppendLine();
                status.AppendFormat("             Trigger Value: {0:N2}", TriggerValue);
                status.AppendLine();

                return status.ToString();
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Initializes <see cref="SnmpHost" />.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            ConnectionStringParser parser = new ConnectionStringParser();
            parser.ParseConnectionString(ConnectionString, this);

            InputMeasurementKeys = ParseInputMeasurementKeys(DataSource, false, TriggerPointTag);

            if (InputMeasurementKeys.Length == 0)
                throw new InvalidOperationException($"Specified TriggerPointTag \"{TriggerPointTag}\" not found. Cannot initialize trigger.");

            if (string.IsNullOrWhiteSpace(TriggerAction))
                throw new InvalidOperationException("No TriggerAction specified. Cannot initialize trigger.");

            // Define trigger operation
            m_triggerOperation = new ShortSynchronizedOperation(TriggerOperation, ex => OnProcessException(MessageLevel.Warning, ex));
        }

        /// <summary>
        /// Gets a short one-line status of this <see cref="T:GSF.TimeSeries.Adapters.AdapterBase" />.
        /// </summary>
        /// <param name="maxLength">Maximum number of available characters for display.</param>
        /// <returns>A short one-line summary of the current status of this <see cref="T:GSF.TimeSeries.Adapters.AdapterBase" />.</returns>
        public override string GetShortStatus(int maxLength)
        {
            if (InputMeasurementKeys.Length > 0)
                return $"Listening for trigger value {TriggerValue:N2} from point tag \"{TriggerPointTag}\"...".CenterText(maxLength);

            return $"Not currently listening for trigger, validate point tag \"{TriggerPointTag}\"...".CenterText(maxLength);
        }

        /// <summary>
        /// Queues a collection of measurements for processing.
        /// </summary>
        /// <param name="measurements">Measurements to queue for processing.</param>
        public override void QueueMeasurementsForProcessing(IEnumerable<IMeasurement> measurements)
        {
            base.QueueMeasurementsForProcessing(measurements);

            IMeasurement measurement = measurements.FirstOrDefault(m => m.ID == InputMeasurementKeys[0].SignalID);

            if (measurement == null)
                return;

            bool triggered = measurement.AdjustedValue == TriggerValue;

            if (triggered && (m_initialValueReceived || TriggerFromInitialValue))
                m_triggerOperation.TryRunOnceAsync();

            m_initialValueReceived = true;
        }

        private void TriggerOperation()
        {
            if (IsDisposed)
                return;

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, TriggerAction);

            string username = DeriveCredential(TriggerActionUserName);
            string password = DeriveCredential(TriggerActionPassword);

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            try
            {
                Task<HttpResponseMessage> task = s_http.SendAsync(request);
                task.Wait();

                HttpResponseMessage response = task.Result;

                if (response.StatusCode == HttpStatusCode.OK)
                    OnStatusMessage(MessageLevel.Info, $"EVENT TRIGGER: Successfully executed trigger action \"{TriggerAction}\" for detected event: \"{TriggerPointTag}\" = {TriggerValue}");
                else
                    throw new Exception($"ERROR: Failed to process trigger action for detected event, HTTP response = {response.StatusCode}: {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                if (ex is AggregateException aggEx)
                    OnProcessException(MessageLevel.Error, new Exception($"ERROR: Failed to process trigger action for detected event: {string.Join(", ", aggEx.InnerExceptions.Select(innerEx => innerEx.Message))}"));
                else
                    OnProcessException(MessageLevel.Error, ex);
            }
        }

        private string DeriveCredential(string credential)
        {
            if (credential == null || !credential.StartsWith(EnvPrefixToken) || credential.Length <= EnvPrefixToken.Length)
                return credential;

            credential = credential.Substring(EnvPrefixToken.Length);

            try
            {
                return Environment.GetEnvironmentVariable(credential);
            }
            catch (SecurityException ex)
            {
                OnProcessException(MessageLevel.Error, new Exception($"Cannot read environmental variable \"{credential}\": {ex.Message}", ex));
            }

            return null;
        }

        #endregion

        #region [ Static ]

        // Static Fields
        private static readonly HttpClient s_http = new HttpClient(new HttpClientHandler { UseCookies = false });

        #endregion
    }
}
