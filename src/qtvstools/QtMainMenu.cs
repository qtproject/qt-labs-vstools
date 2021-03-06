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

using EnvDTE;
using Microsoft.VisualStudio.Shell;
using QtVsTools.Core;
using QtVsTools.VisualStudio;
using System;
using System.ComponentModel.Design;
using System.Windows.Forms;

namespace QtVsTools
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class QtMainMenu
    {
        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid MainMenuGuid = new Guid("58f83fff-d39d-4c66-810b-2702e1f04e73");

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static QtMainMenu Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new QtMainMenu(package);
        }

        /// <summary>
        /// Command ID.
        /// </summary>
        private enum CommandId
        {
            QtVersionId = 0x0500,
            ViewQtHelpId = 0x0501,
            LaunchDesignerId = 0x0100,
            LaunchLinguistId = 0x0101,
            OpenProFileId = 0x0102,
            ImportPriFileId = 0x0103,
            ExportPriFileId = 0x0104,
            ExportProFileId = 0x0105,
            CreateNewTsFileId = 0x0107,
            ConvertToQtMsBuild = 0x0130,
            ConvertToQtId = 0x0124,
            ConvertToQmakeId = 0x0108,
            QtProjectSettingsId = 0x0109,
            ChangeProjectQtVersionId = 0x0126,
            QtOptionsId = 0x0110,
            QtVersionsId = 0x0111,
        }

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package m_package;

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get { return m_package; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QtMainMenu"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private QtMainMenu(Package package)
        {
            if (package == null)
                throw new ArgumentNullException("package");

            m_package = package;

            var commandService = VsServiceProvider
                .GetService<IMenuCommandService, OleMenuCommandService>();
            if (commandService == null)
                return;

            foreach (var id in Enum.GetValues(typeof(CommandId))) {
                var command = new OleMenuCommand(execHandler,
                    new CommandID(MainMenuGuid, (int) id));
                command.BeforeQueryStatus += beforeQueryStatus;
                commandService.AddCommand(command);
            }
        }

        private void execHandler(object sender, EventArgs e)
        {
            var command = sender as OleMenuCommand;
            if (command == null)
                return;

            switch ((CommandId) command.CommandID.ID) {
            case CommandId.ViewQtHelpId:
                VsShellUtilities.OpenSystemBrowser("https://www.qt.io/developers");
                break;
            case CommandId.LaunchDesignerId:
                Vsix.Instance.QtDesigner.Start(hideWindow: false);
                break;
            case CommandId.LaunchLinguistId:
                Vsix.Instance.QtLinguist.Start(hideWindow: false);
                break;
            case CommandId.OpenProFileId:
                ExtLoader.ImportProFile();
                break;
            case CommandId.ImportPriFileId:
                ExtLoader.ImportPriFile(HelperFunctions.GetSelectedQtProject(Vsix.Instance.Dte));
                break;
            case CommandId.ExportPriFileId:
                ExtLoader.ExportPriFile();
                break;
            case CommandId.ExportProFileId:
                ExtLoader.ExportProFile();
                break;
            case CommandId.CreateNewTsFileId:
                Translation.CreateNewTranslationFile(HelperFunctions.GetSelectedQtProject(Vsix
                    .Instance.Dte));
                break;
            case CommandId.ConvertToQtId:
            case CommandId.ConvertToQmakeId:
                {
                    var caption = SR.GetString("ConvertTitle");
                    var text = SR.GetString("ConvertConfirmation");
                    if (MessageBox.Show(text, caption, MessageBoxButtons.YesNo) == DialogResult.Yes) {
                        HelperFunctions.ToggleProjectKind(HelperFunctions.GetSelectedProject(Vsix
                            .Instance.Dte));
                    }
                }
                break;
            case CommandId.ConvertToQtMsBuild:
                {
                    QtMsBuildConverter.SolutionToQtMsBuild();
                }
                break;
            case CommandId.QtProjectSettingsId:
                {
                    var pro = HelperFunctions.GetSelectedQtProject(Vsix.Instance.Dte);
                    int projectVersion = QtProject.GetFormatVersion(pro);
                    if (projectVersion >= Resources.qtMinFormatVersion_Settings) {
                        Vsix.Instance.Dte.ExecuteCommand("Project.Properties");
                    } else if (pro != null) {
                        using (var formProjectQtSettings = new FormProjectQtSettings()) {
                            formProjectQtSettings.SetProject(pro);
                            formProjectQtSettings.StartPosition = FormStartPosition.CenterParent;
                            var ww = new MainWinWrapper(Vsix.Instance.Dte);
                            formProjectQtSettings.ShowDialog(ww);
                        }
                    } else {
                        MessageBox.Show(SR.GetString("NoProjectOpened"));
                    }
                }
                break;
            case CommandId.ChangeProjectQtVersionId:
                {
                    var pro = HelperFunctions.GetSelectedQtProject(Vsix.Instance.Dte);
                    if (HelperFunctions.IsQMakeProject(pro)) {
                        using (var formChangeQtVersion = new FormChangeQtVersion()) {
                            formChangeQtVersion.UpdateContent(ChangeFor.Project);
                            var ww = new MainWinWrapper(Vsix.Instance.Dte);
                            if (formChangeQtVersion.ShowDialog(ww) == DialogResult.OK) {
                                var qtVersion = formChangeQtVersion.GetSelectedQtVersion();
                                HelperFunctions.SetDebuggingEnvironment(pro, "PATH=" + QtVersionManager
                                    .The().GetInstallPath(qtVersion) + @"\bin;$(PATH)", true);
                            }
                        }
                    }
                }
                break;
            case CommandId.QtOptionsId:
                Vsix.Instance.ShowOptionPage(typeof(Options.QtOptionsPage));
                break;
            case CommandId.QtVersionsId:
                Vsix.Instance.ShowOptionPage(typeof(Options.QtVersionsPage));
                break;
            }
        }

        private void beforeQueryStatus(object sender, EventArgs e)
        {
            var command = sender as OleMenuCommand;
            if (command == null)
                return;

            switch ((CommandId) command.CommandID.ID) {
            case CommandId.ViewQtHelpId:
                command.Visible = command.Enabled = true;
                break;
            case CommandId.QtVersionId:
                command.Text = "Qt Visual Studio Tools version " + Version.USER_VERSION;
                command.Visible = true;
                command.Enabled = false;
                break;
            case CommandId.LaunchDesignerId:
            case CommandId.LaunchLinguistId:
            case CommandId.OpenProFileId:
            case CommandId.QtOptionsId:
            case CommandId.QtVersionsId:
                command.Visible = true;
                command.Enabled = true;
                break;
            case CommandId.ImportPriFileId:
            case CommandId.ExportPriFileId:
            case CommandId.ExportProFileId:
            case CommandId.CreateNewTsFileId:
                {
                    command.Visible = true;
                    command.Enabled = HelperFunctions.IsQtProject(HelperFunctions
                        .GetSelectedProject(Vsix.Instance.Dte));
                }
                break;
            // TODO: Fix these functionality and re-enable the menu items
            case CommandId.ConvertToQtId:
            case CommandId.ConvertToQmakeId:
                {
                    command.Visible = false;
                }
                break;
            //case CommandId.ConvertToQmakeId:
            case CommandId.QtProjectSettingsId:
                {
                    var status = vsCommandStatus.vsCommandStatusSupported;
                    var project = HelperFunctions.GetSelectedProject(Vsix.Instance.Dte);
                    if (project != null) {
                        if (HelperFunctions.IsQtProject(project))
                            status |= vsCommandStatus.vsCommandStatusEnabled;
                        else if (HelperFunctions.IsQMakeProject(project))
                            status |= vsCommandStatus.vsCommandStatusInvisible;
                    }
                    command.Enabled = ((status & vsCommandStatus.vsCommandStatusEnabled) != 0);
                    command.Visible = ((status & vsCommandStatus.vsCommandStatusInvisible) == 0);
                }
                break;
            //case CommandId.ConvertToQtId:
            case CommandId.ChangeProjectQtVersionId:
                {
                    var status = vsCommandStatus.vsCommandStatusSupported;
                    var project = HelperFunctions.GetSelectedProject(Vsix.Instance.Dte);
                    if ((project == null) || HelperFunctions.IsQtProject(project))
                        status |= vsCommandStatus.vsCommandStatusInvisible;
                    else if (HelperFunctions.IsQMakeProject(project))
                        status |= vsCommandStatus.vsCommandStatusEnabled;
                    else
                        status |= vsCommandStatus.vsCommandStatusInvisible;
                    command.Enabled = ((status & vsCommandStatus.vsCommandStatusEnabled) != 0);
                    command.Visible = ((status & vsCommandStatus.vsCommandStatusInvisible) == 0);
                }
                break;
            case CommandId.ConvertToQtMsBuild:
                {
                    command.Visible = true;
                    command.Enabled = (Vsix.Instance.Dte.Solution != null
                        && Vsix.Instance.Dte.Solution.Projects != null
                        && Vsix.Instance.Dte.Solution.Projects.Count > 0);
                }
                break;
            }
        }
    }
}
