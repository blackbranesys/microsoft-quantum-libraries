// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
#nullable enable

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System;

using Broombridge = Microsoft.Quantum.Chemistry.Broombridge;

namespace Microsoft.Quantum.Chemistry.Tools
{

    public static class Convert
    {
        public static Command CreateCommand() =>
            new Command("convert")
            {
                new Argument<FileInfo>(
                    "path",
                    "Input data to be loaded, or - to load from stdin."
                ),
                new Option<SerializationFormat>(
                    "--from",
                    "Format to use in loading problem description data."
                ),
                new Option<SerializationFormat>(
                    "--to",
                    "Format to use in writing problem description data."
                ),
                new Option<FileInfo?>(
                    "--out",
                    "Path to write output to. Data will be written to stdout by default."
                )
            }
            .WithDescription(
                "Converts between various problem description formats. " +
                "Note that not all data is supported by all formats, such that data may be lost " +
                "when converting to formats other than Broombridge."
            )
            .WithHandler<FileInfo, SerializationFormat, SerializationFormat, FileInfo?>(
                (path, from, to, @out) =>
                {
                    using var reader =
                        path.Name == "-"
                        ? System.Console.In
                        : File.OpenText(path.FullName);
                    using var writer =
                        @out == null
                        ? System.Console.Out
                        : new StreamWriter(File.OpenWrite(@out.FullName));
                    ConvertProblemDescription(reader, from, writer, to);
                }
            );

        public static void ConvertProblemDescription(
            TextReader reader, SerializationFormat from,
            TextWriter writer, SerializationFormat to
        )
        {
            // Our strategy will be to load whatever our source data is into an
            // in-memory instance of Broombridge, then re-export that to
            // the destination format.
            // In doing so, we'll use the latest version of Broombridge
            // to represent data between loading and saving.
            var data = Load(reader, from);
            Save(data, writer, to);
        }

        internal static Broombridge.V0_2.Data Load(TextReader reader, SerializationFormat from) =>
            from switch
            {
                SerializationFormat.Broombridge => Broombridge
                    .Deserializers
                    .DeserializeBroombridge(reader)
                    .Raw,
                SerializationFormat.LiQuiD =>
                    new Broombridge.V0_2.Data
                    {
                        ProblemDescriptions =
                            LiQuiD
                            .Deserialize(reader)
                            .Select(liquidDescription =>
                                new Broombridge.V0_2.ProblemDescription
                                {
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["misc_info"] = liquidDescription.MiscellaneousInformation
                                    },
                                    // TODO: add command line option for controlling units.
                                    CoulombRepulsion = new Broombridge.V0_1.SimpleQuantity
                                    {
                                        Value = liquidDescription.CoulombRepulsion,
                                        Units = "hartree"
                                    },
                                    NOrbitals = liquidDescription.NOrbitals,
                                    NElectrons = liquidDescription.NElectrons,
                                    Hamiltonian = new Broombridge.V0_1.HamiltonianData
                                    {
                                        OneElectronIntegrals = liquidDescription.IndiciesToArrayQuantity(
                                            TermType.OrbitalIntegral.OneBody
                                        ),
                                        TwoElectronIntegrals = liquidDescription.IndiciesToArrayQuantity(
                                            TermType.OrbitalIntegral.TwoBody
                                        )
                                    },
                                    EnergyOffset = new Broombridge.V0_1.SimpleQuantity
                                    {
                                        Value = /* FIXME: liquidDescription
                                            .OrbitalIntegralHamiltonian
                                            .Terms[TermType.OrbitalIntegral.Identity]
                                            .Value*/ 0.0,
                                        Units = "hartree"
                                    }
                                }
                            )
                            .ToList()
                    }
                    .WithDefaultMetadata(),
                SerializationFormat.FciDump =>
                    new Broombridge.V0_2.Data
                    {
                        ProblemDescriptions = new List<Broombridge.V0_2.ProblemDescription>
                        {
                            // TODO: add FCIDUMP logic here.
                        }
                    }
                    .WithDefaultMetadata(),
                _ => throw new ArgumentException($"Invalid format {from}.")
            };

        internal static void Save(Broombridge.V0_2.Data data, TextWriter writer, SerializationFormat to)
        {
            switch (to)
            {
                case SerializationFormat.Broombridge:
                    Broombridge.Serializers.SerializeBroombridgev0_2(data, writer);
                    return;
                case SerializationFormat.LiQuiD:
                    throw new NotSupportedException("Not yet implemented.");
                    return;
                case SerializationFormat.FciDump:
                    throw new NotSupportedException("Not yet implemented.");
                    return;
                default:
                    throw new ArgumentException($"Invalid format {to}.");
                    return;
            };
        }
    }   
 
}