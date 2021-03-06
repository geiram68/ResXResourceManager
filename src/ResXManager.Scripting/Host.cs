﻿namespace ResXManager.Scripting
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.ComponentModel.Composition.Hosting;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using JetBrains.Annotations;

    using ResXManager.Infrastructure;
    using ResXManager.Model;

    public sealed class Host : IDisposable
    {
        private readonly AggregateCatalog _compositionCatalog;
        private readonly CompositionContainer _compositionContainer;
        private readonly SourceFilesProvider _sourceFilesProvider;

        public Host()
        {
            var assembly = GetType().Assembly;

            _compositionCatalog = new AggregateCatalog();
            _compositionContainer = new CompositionContainer(_compositionCatalog, true);
#pragma warning disable CA2000 // Dispose objects before losing scope => AggregateCatalog will dispose all
            // ReSharper disable RedundantNameQualifier
            _compositionCatalog.Catalogs.Add(new AssemblyCatalog(assembly));
            _compositionCatalog.Catalogs.Add(new AssemblyCatalog(typeof(Infrastructure.Properties.AssemblyKey).Assembly));
            _compositionCatalog.Catalogs.Add(new AssemblyCatalog(typeof(Model.Properties.AssemblyKey).Assembly));
#pragma warning restore CA2000 // Dispose objects before losing scope
            // ReSharper restore RedundantNameQualifier

            _sourceFilesProvider = _compositionContainer.GetExportedValue<SourceFilesProvider>();
            ResourceManager = _compositionContainer.GetExportedValue<ResourceManager>();
            ResourceManager.BeginEditing += ResourceManager_BeginEditing;

            Configuration = _compositionContainer.GetExportedValue<Configuration>();
        }

        [NotNull]
        public ResourceManager ResourceManager { get; }

        public void Load(string? folder, string? exclusionFilter = @"Migrations\\\d{15}")
        {
            _sourceFilesProvider.SolutionFolder = folder;
            _sourceFilesProvider.ExclusionFilter = exclusionFilter;

            ResourceManager.Reload();
        }

        public void Save()
        {
            ResourceManager.Save();
        }

        public void ExportExcel([NotNull] string filePath)
        {
            ExportExcel(filePath, null);
        }

        public void ExportExcel([NotNull] string filePath, object? entries)
        {
            ExportExcel(filePath, entries as IEnumerable<object>, null);
        }

        public void ExportExcel([NotNull] string filePath, object? entries, object? languages)
        {
            ExportExcel(filePath, entries, languages, null);
        }

        public void ExportExcel([NotNull] string filePath, object? entries, object? languages, object? comments)
        {
            ExportExcel(filePath, entries, languages, comments, ExcelExportMode.SingleSheet);
        }

        public void ExportExcel([NotNull] string filePath, object? entries, object? languages, object? comments, ExcelExportMode exportMode)
        {
            var resourceScope = new ResourceScope(
                entries ?? ResourceManager.TableEntries,
                languages ?? ResourceManager.Cultures,
                comments ?? Array.Empty<object>());

            ResourceManager.ExportExcelFile(filePath, resourceScope, exportMode);
        }

        public void ImportExcel([NotNull] string filePath)
        {
            var changes = ResourceManager.ImportExcelFile(filePath);

            changes.Apply();
        }

        [NotNull]
        public string CreateSnapshot()
        {
            return ResourceManager.CreateSnapshot();
        }

        public void LoadSnapshot(string? value)
        {
            ResourceManager.LoadSnapshot(value);
        }

        public void Dispose()
        {
            _compositionCatalog.Dispose();
            _compositionContainer.Dispose();
        }

        private void ResourceManager_BeginEditing([NotNull] object sender, [NotNull] ResourceBeginEditingEventArgs e)
        {
            if (!CanEdit(e.Entity, e.CultureKey))
            {
                e.Cancel = true;
            }
        }

        private bool CanEdit([NotNull] ResourceEntity entity, CultureKey? cultureKey)
        {
            if (cultureKey == null)
                return true;

            var rootFolder = _sourceFilesProvider.SolutionFolder;
            if (rootFolder == null || string.IsNullOrEmpty(rootFolder))
                return false;

            var language = entity.Languages.FirstOrDefault(lang => cultureKey.Equals(lang.CultureKey));

            if (language != null)
                return true;

            var culture = cultureKey.Culture;

            if (culture == null)
                return false; // no neutral culture => this should never happen.

            var neutralLanguage = entity.Languages.FirstOrDefault();
            if (neutralLanguage == null)
                return false;

            var languageFileName = neutralLanguage.ProjectFile.GetLanguageFileName(culture);

            if (!File.Exists(languageFileName))
            {
                var directoryName = Path.GetDirectoryName(languageFileName);
                if (!string.IsNullOrEmpty(directoryName))
                    Directory.CreateDirectory(directoryName);

                File.WriteAllText(languageFileName, Model.Properties.Resources.EmptyResxTemplate);
            }

            entity.AddLanguage(new ProjectFile(languageFileName, rootFolder, entity.ProjectName, null));

            return true;
        }

        [NotNull]
        public Configuration Configuration { get; }
    }

    [Export(typeof(IConfiguration))]
    [Export(typeof(Configuration))]
    public class Configuration : IConfiguration
    {
        public bool SaveFilesImmediatelyUponChange => false;

        public CultureInfo NeutralResourcesLanguage { get; set; } = new CultureInfo("en-US");

        public StringComparison? EffectiveResXSortingComparison { get; set; }

        public DuplicateKeyHandling DuplicateKeyHandling { get; set; }

        public ResourceTableEntryRules Rules { get; } = new ResourceTableEntryRules();

        public bool RemoveEmptyEntries { get; set; }
    }
}
