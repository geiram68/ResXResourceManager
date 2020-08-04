﻿namespace ResXManager.VSIX
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using ResXManager.Infrastructure;
    using ResXManager.Model;

    using TomsToolbox.Essentials;

    internal class DteProjectFile : ProjectFile
    {
        public const string T4FileName = "Resources.Designer.t4";

        private readonly DteSolution _solution;

        private readonly List<EnvDTE.ProjectItem> _projectItems = new List<EnvDTE.ProjectItem>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DteProjectFile" /> class.
        /// </summary>
        /// <param name="solution">The solution.</param>
        /// <param name="solutionFolder">The solution folder.</param>
        /// <param name="filePath">Name of the file.</param>
        /// <param name="projectName">Name of the project.</param>
        /// <param name="uniqueProjectName">Unique name of the project file.</param>
        /// <param name="projectItem">The project item, or null if the projectItem is not known.</param>
        public DteProjectFile(DteSolution solution, string solutionFolder, string filePath, string? projectName, string? uniqueProjectName, EnvDTE.ProjectItem projectItem)
            : base(filePath, solutionFolder, projectName, uniqueProjectName)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            _solution = solution;
            _projectItems.Add(projectItem);
        }

        /// <summary>
        /// Gets the project items.
        /// </summary>
        public IList<EnvDTE.ProjectItem> ProjectItems => _projectItems;

        public void AddProject(string projectName, EnvDTE.ProjectItem projectItem)
        {
            _projectItems.Add(projectItem);
            ProjectName += @", " + projectName;
        }

        public CodeGenerator CodeGenerator
        {
            get
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

                return GetCodeGenerator();
            }
            set
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

                if (GetCodeGenerator() != value)
                {
                    SetCodeGenerator(value);
                }

                OnPropertyChanged();
            }
        }

        public EnvDTE.ProjectItem DefaultProjectItem
        {
            get
            {
                var item = ProjectItems.First();
                return item;
            }
        }

        public EnvDTE.ProjectItem? ParentItem
        {
            get
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

                try
                {
                    return DefaultProjectItem.Collection?.Parent as EnvDTE.ProjectItem;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public override bool IsWinFormsDesignerResource
        {
            get
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

                try
                {
                    var projectItem = DefaultProjectItem;
                    var projectItems = projectItem.Collection;

                    var parent = projectItems?.Parent as EnvDTE.ProjectItem;
                    var subType = parent?.GetProperty(@"SubType") as string;

                    // https://microsoft.public.de.german.entwickler.dotnet.vstudio.narkive.com/nL9BqJlj/aus-subtype-form-wird-subtype-component
                    return (subType == @"Form") || (subType == @"UserControl") || (subType == @"Component");
                }
                catch (ExternalException)
                {
                }

                return false;
            }
        }

        private CodeGenerator GetCodeGenerator()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var projectItem = DefaultProjectItem;
                var containingProject = projectItem.ContainingProject;

                if ((containingProject == null) || (containingProject.Kind != ItemKind.CSharpProject))
                    return CodeGenerator.None;

                var customTool = projectItem.GetCustomTool();

                if (customTool.IsNullOrEmpty())
                {
                    if (IsWinFormsDesignerResource)
                        return CodeGenerator.WinForms;

                    return projectItem.Children().Any(IsTextTemplate) ? CodeGenerator.TextTemplate : CodeGenerator.None;
                }

                return Enum.TryParse(customTool, out CodeGenerator codeGenerator) ? codeGenerator : CodeGenerator.Unknown;
            }
            catch (ExternalException)
            {
            }

            return CodeGenerator.Unknown;
        }

        private void SetCodeGenerator(CodeGenerator value)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                foreach (var projectItem in ProjectItems)
                {
                    var containingProject = projectItem.ContainingProject;

                    if ((containingProject == null) || (containingProject.Kind != ItemKind.CSharpProject))
                        return;

                    switch (value)
                    {
                        case CodeGenerator.ResXFileCodeGenerator:
                        case CodeGenerator.PublicResXFileCodeGenerator:
                            SetCustomToolCodeGenerator(projectItem, value);
                            break;

                        case CodeGenerator.TextTemplate:
                            SetTextTemplateCodeGenerator(projectItem);
                            break;
                    }
                }
            }
            catch (ExternalException)
            {
            }
        }

        private static bool IsTextTemplate(EnvDTE.ProjectItem item)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var name = item.Name;

            return (name != null) && name.EndsWith(@".tt", StringComparison.OrdinalIgnoreCase);
        }

        private void SetTextTemplateCodeGenerator(EnvDTE.ProjectItem projectItem)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            projectItem.SetCustomTool(null);

            if (_solution.GetCachedProjectFiles()?.Any(file => file.RelativeFilePath.Equals(T4FileName, StringComparison.OrdinalIgnoreCase)) != true)
            {
                var fullName = Path.Combine(_solution.SolutionFolder, T4FileName);
                File.WriteAllBytes(fullName, Resources.Resources_Designer_t4);
                _solution.AddFile(fullName);
            }

            // Ensure DataAnnotations is referenced, used by TT generated code.
            const string dataAnnotations = "System.ComponentModel.DataAnnotations";

            var vsProject = projectItem.ContainingProject?.Object as VSLangProj.VSProject;
            vsProject?.References?.Add(dataAnnotations);

            var fileName = Path.ChangeExtension(FilePath, "Designer.tt");

            File.WriteAllBytes(fileName, Resources.Resources_Designer_tt);

            var item = projectItem.AddFromFile(fileName);
            if (item == null)
                return;

            item.SetProperty(@"BuildAction", 0);

            item.RunCustomTool();
        }

        private static void SetCustomToolCodeGenerator(EnvDTE.ProjectItem projectItem, CodeGenerator value)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

#pragma warning disable VSTHRD010 // Accessing ... should only be done on the main thread.
            projectItem.Children()
                .Where(IsTextTemplate)
                .ToArray()
                .ForEach(i => i.Delete());
#pragma warning restore VSTHRD010 // Accessing ... should only be done on the main thread.

            projectItem.SetCustomTool(value.ToString());
        }
    }
}