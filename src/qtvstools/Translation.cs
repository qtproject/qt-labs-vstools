/****************************************************************************
**
** Copyright (C) 2016 The Qt Company Ltd.
** Contact: https://www.qt.io/licensing/
**
** This file is part of the Qt VS Tools.
**
** $QT_BEGIN_LICENSE:GPL-EXCEPT$
** Commercial License Usage
** Licensees holding valid commercial Qt licenses may use this file in
** accordance with the commercial license agreement provided with the
** Software or, alternatively, in accordance with the terms contained in
** a written agreement between you and The Qt Company. For licensing terms
** and conditions see https://www.qt.io/terms-conditions. For further
** information use the contact form at https://www.qt.io/contact-us.
**
** GNU General Public License Usage
** Alternatively, this file may be used under the terms of the GNU
** General Public License version 3 as published by the Free Software
** Foundation with exceptions as appearing in the file LICENSE.GPL3-EXCEPT
** included in the packaging of this file. Please review the following
** information to ensure the GNU General Public License requirements will
** be met: https://www.gnu.org/licenses/gpl-3.0.html.
**
** $QT_END_LICENSE$
**
****************************************************************************/

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.VCProjectEngine;
using QtVsTools.Core;
using QtVsTools.VisualStudio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QtVsTools
{
    using static Core.HelperFunctions;
    using QtMsBuild;

    /// <summary>
    /// Run Qt translation tools by invoking the corresponding Qt/MSBuild targets
    /// </summary>
    public static class Translation
    {
        public static void RunlRelease(VCFile vcFile)
        {
            var vcProj = vcFile.project as VCProject;
            var project = vcProj?.Object as EnvDTE.Project;
            RunTranslationTarget(BuildAction.Release,
                project, new[] { vcFile.RelativePath });
        }

        public static void RunlRelease(VCFile[] vcFiles)
        {
            var vcProj = vcFiles.FirstOrDefault()?.project as VCProject;
            var project = vcProj?.Object as EnvDTE.Project;
            RunTranslationTarget(BuildAction.Release,
                project, vcFiles.Select(vcFile => vcFile?.RelativePath));
        }

        public static void RunlRelease(EnvDTE.Project project)
        {
            RunTranslationTarget(BuildAction.Release, project);
        }

        public static void RunlRelease(EnvDTE.Solution solution)
        {
            if (solution == null)
                return;

            foreach (var project in HelperFunctions.ProjectsInSolution(solution.DTE))
                RunlRelease(project);
        }

        public static void RunlUpdate(VCFile vcFile)
        {
            var vcProj = vcFile.project as VCProject;
            var project = vcProj?.Object as EnvDTE.Project;
            RunTranslationTarget(BuildAction.Update,
                project, new[] { vcFile.RelativePath });
        }

        public static void RunlUpdate(VCFile[] vcFiles)
        {
            var vcProj = vcFiles.FirstOrDefault()?.project as VCProject;
            var project = vcProj?.Object as EnvDTE.Project;
            RunTranslationTarget(BuildAction.Update,
                project, vcFiles.Select(vcFile => vcFile?.RelativePath));
        }

        public static void RunlUpdate(EnvDTE.Project project)
        {
            RunTranslationTarget(BuildAction.Update, project);
        }

        enum BuildAction { Update, Release }

        static void RunTranslationTarget(
            BuildAction buildAction,
            EnvDTE.Project project,
            IEnumerable<string> selectedFiles = null)
        {
            using (WaitDialog.Start(
                "Qt Visual Studio Tools", "Running translation tool...")) {

                var qtPro = QtProject.Create(project);
                if (project == null || qtPro == null) {
                    Messages.Print(
                        "translation: Error accessing project interface");
                    return;
                }

                if (qtPro.FormatVersion < Resources.qtMinFormatVersion_Settings) {
                    Messages.Print("translation: Legacy project format");
                    try {
                        Legacy_RunTranslation(buildAction, qtPro, selectedFiles);
                    } catch (Exception e) {
                        Messages.Print(
                            e.Message + "\r\n\r\nStacktrace:\r\n" + e.StackTrace);
                    }
                    return;
                }

                var activeConfig = project.ConfigurationManager?.ActiveConfiguration;
                if (activeConfig == null) {
                    Messages.Print(
                        "translation: Error accessing build interface");
                    return;
                }
                var activeConfigId = string.Format("{0}|{1}",
                    activeConfig.ConfigurationName, activeConfig.PlatformName);

                var target = "QtTranslation";
                var properties = new Dictionary<string, string>();
                switch (buildAction) {
                    case BuildAction.Update:
                        properties["QtTranslationForceUpdate"] = "true";
                        break;
                    case BuildAction.Release:
                        properties["QtTranslationForceRelease"] = "true";
                        break;
                }
                if (selectedFiles != null)
                    properties["SelectedFiles"] = string.Join(";", selectedFiles);

                QtProjectBuild.StartBuild(project, activeConfigId, properties, new[] { target });
            }
        }

        public static void RunlUpdate(EnvDTE.Solution solution)
        {
            if (solution == null)
                return;

            foreach (var project in HelperFunctions.ProjectsInSolution(solution.DTE))
                RunlUpdate(project);
        }

        static void Legacy_RunTranslation(
            BuildAction buildAction,
            QtProject qtProject,
            IEnumerable<string> tsFiles)
        {
            if (tsFiles == null) {
                tsFiles = (qtProject.VCProject
                    .GetFilesEndingWith(".ts") as IVCCollection)
                    .Cast<VCFile>()
                    .Select(vcFile => vcFile.RelativePath);
                if (tsFiles == null) {
                    Messages.Print("translation: no translation files found");
                    return;
                }
            }
            string tempFile = null;
            foreach (var file in tsFiles.Where(file => file != null))
                Legacy_RunTranslation(buildAction, qtProject, file, ref tempFile);
        }

        static void Legacy_RunTranslation(
            BuildAction buildAction,
            QtProject qtProject,
            string tsFile,
            ref string tempFile)
        {
            var qtVersion = qtProject.GetQtVersion();
            var qtInstallPath = QtVersionManager.The().GetInstallPath(qtVersion);
            if (string.IsNullOrEmpty(qtInstallPath)) {
                Messages.Print("translation: Error accessing Qt installation");
                return;
            }

            var procInfo = new ProcessStartInfo
            {
                WorkingDirectory = qtProject.ProjectDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                Arguments = ""
            };
            switch (buildAction) {
                case BuildAction.Update:
                    Messages.Print("\r\n--- (lupdate) file: " + tsFile);
                    procInfo.FileName = Path.Combine(qtInstallPath, "bin", "lupdate.exe");
                    var options = QtVSIPSettings.GetLUpdateOptions();
                    if (!string.IsNullOrEmpty(options))
                        procInfo.Arguments += options + " ";
                    if (tempFile == null) {
                        var inputFiles = GetProjectFiles(qtProject.Project, FilesToList.FL_HFiles)
                            .Union(GetProjectFiles(qtProject.Project, FilesToList.FL_CppFiles))
                            .Union(GetProjectFiles(qtProject.Project, FilesToList.FL_UiFiles));
                        tempFile = Path.GetTempFileName();
                        File.WriteAllLines(tempFile, inputFiles);
                    }
                    procInfo.Arguments += string.Format("\"@{0}\" -ts \"{1}\"", tempFile, tsFile);
                    break;
                case BuildAction.Release:
                    Messages.Print("\r\n--- (lrelease) file: " + tsFile);
                    procInfo.FileName = Path.Combine(qtInstallPath, "bin", "lrelease.exe");
                    options = QtVSIPSettings.GetLReleaseOptions();
                    if (!string.IsNullOrEmpty(options))
                        procInfo.Arguments += options + " ";
                    procInfo.Arguments += string.Format("\"{0}\"", tsFile);
                    break;
            }
            using (var proc = Process.Start(procInfo)) {
                proc.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Messages.Print(e.Data);
                };
                proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Messages.Print(e.Data);
                };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                switch (proc.ExitCode) {
                    case 0:
                        Messages.Print("translation: ok");
                        break;
                    default:
                        Messages.Print(string.Format("translation: ERROR {0}", proc.ExitCode));
                        break;
                }
            }
        }

        public static void CreateNewTranslationFile(EnvDTE.Project project)
        {
            if (project == null)
                return;

            using (var transDlg = new AddTranslationDialog(project)) {
                if (transDlg.ShowDialog() == DialogResult.OK) {
                    try {
                        var qtPro = QtProject.Create(project);
                        var file = qtPro.AddFileInFilter(Filters.TranslationFiles(),
                            transDlg.TranslationFile, true);
                        RunlUpdate(file);
                    } catch (QtVSException e) {
                        Messages.DisplayErrorMessage(e.Message);
                    } catch (System.Exception ex) {
                        Messages.DisplayErrorMessage(ex.Message);
                    }
                }
            }
        }
    }
}
