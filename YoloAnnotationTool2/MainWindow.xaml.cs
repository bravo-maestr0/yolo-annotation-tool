using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using YoloAnnotationTool2.ViewModels;
using System.IO;
using System.ComponentModel;
using Microsoft.Win32;
using System.Globalization;
using System.Windows.Annotations;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using YoloAnnotationTool2.Drawing;
using YoloAnnotationTool2.Enums;
using System.Xml;
using System.Text.Json;
using System.Runtime;
using YoloAnnotationTool2;
using System.IO.Compression;
using Point = System.Windows.Point;
using Path = System.IO.Path;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Rectangle = System.Windows.Shapes.Rectangle;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using System.Reflection.Emit;
using System.Drawing;
using YoloAnnotationTool2.Models;
using YoloAnnotationTool2.Services;
using Color = System.Windows.Media.Color;


namespace YoloAnnotationTool

{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; set; }
        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new MainViewModel();
 
            DataContext = ViewModel;
            ViewModel.saveStatus = "\u2B55";
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            addTrain.IsEnabled = false;
            addVal.IsEnabled = false;
            addTest.IsEnabled = false;

            this.KeyDown += MainWindow_KeyDown;
            this.Focusable = true;
            this.Focus();

            var settings = SettingsManager.Load();

            if (!string.IsNullOrWhiteSpace(settings.SaveFolderPath))
            {
                ViewModel.LabelText = $"{settings.SaveFolderPath}";
            }
            else
            {
                ViewModel.LabelText= "The project directory is not selected";
            }
        }
        private void MenuItemOpen_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the project folder to open";
                var settings = SettingsManager.Load();
                dialog.SelectedPath = settings.SaveFolderPath;
                DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    ViewModel.drawnRectangles.Clear();
                    ViewModel.ClassLabels.Clear();
                    ViewModel.SelectedImageName = null;
                    ViewModel.CurrentLabelColor = System.Windows.Media.Brushes.White;
                    Image1.Source = null;
                    DrawCanvas.Children.Clear(); 
                    string selectedPath = dialog.SelectedPath;
                    ViewModel.PoseKeypoints.Clear();
                    ViewModel.PoseSkeletonLines.Clear();
                    ViewModel.PosePoints.Clear();
                    ViewModel.AllPosePoints.Clear();
                    ViewModel.pointCounterText = "";
                    settings.CurrentProjectPath = selectedPath;
                    SettingsManager.Save(settings);
                    string lastFolderName = System.IO.Path.GetFileName(selectedPath);
                    ViewModel.projectWithSuffix = lastFolderName;

                    this.SaveButton.IsEnabled = true;
                    this.SaveButton.Opacity = 1.0;
                    this.SaveButton.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF007ACC"));
                    this.AddClass.IsEnabled = true;
                    this.AddClass.Opacity = 1.0;
                    this.AddClass.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF007ACC"));

                    if (EndsWithDetect(lastFolderName))
                    {
                        ViewModel.SelectedAnnotationType = AnnotationType.Detect;
                    }
                    else if (EndsWithOBB(lastFolderName)) 
                    {
                        ViewModel.SelectedAnnotationType = AnnotationType.OBB;
                    }
                    else if (EndsWithSegment(lastFolderName))
                    {
                        ViewModel.SelectedAnnotationType = AnnotationType.Segment;
                    }
                    else if (EndsWithClassify(lastFolderName))
                    {
                        ViewModel.SelectedAnnotationType = AnnotationType.Classify;
                        ViewModel.CurrentProject = lastFolderName;
                        TrainHeader.IsExpanded = false;
                        ValHeader.IsExpanded = false;
                        TestHeader.IsExpanded = false;
                        ViewModel.TrainImages.Clear();
                        ViewModel.ValImages.Clear();
                        ViewModel.TestImages.Clear();


                        this.SaveButton.IsEnabled = false;
                        this.SaveButton.Opacity = 0.5;
                        this.SaveButton.Background = new SolidColorBrush(Colors.Red);

                        System.Windows.MessageBox.Show("Project " + lastFolderName + " is open");

                        ProjectService.LoadClassesClassify(ViewModel, selectedPath);

                        return;
                    }
                    else if(EndsWithPose(lastFolderName)) 
                    {
                        ViewModel.SelectedAnnotationType = AnnotationType.Pose;
                        ViewModel.pointCounterText = $"Point {ViewModel.PoseCurrentPointIndex + 1}/17: Nose";

                        this.AddClass.IsEnabled = false;
                        this.AddClass.Opacity = 0.5;
                        this.AddClass.Background = new SolidColorBrush(Colors.Red);

                        ViewModel.ClassLabels.Add("person");
                        ViewModel.SelectedClassLabel = "person";
                        
                        ViewModel.CurrentProject = lastFolderName;
                        TrainHeader.IsExpanded = false;
                        ValHeader.IsExpanded = false;
                        TestHeader.IsExpanded = false;
                        LoadProjectImages(selectedPath);
                        System.Windows.MessageBox.Show("Project " + lastFolderName + " is open");
                        return;
                    }
                    ViewModel.CurrentProject = lastFolderName;
                    TrainHeader.IsExpanded = false;
                    ValHeader.IsExpanded = false;
                    TestHeader.IsExpanded = false;
                    LoadProjectImages(selectedPath);
                    System.Windows.MessageBox.Show("Project " + lastFolderName + " is open");

                    string colorsPath = System.IO.Path.Combine(settings.CurrentProjectPath, "colors.json");
                    File.SetAttributes(colorsPath, FileAttributes.Normal);
                    ViewModel.ClassColors = LoadClassBrushes(colorsPath);
                    File.SetAttributes(colorsPath, FileAttributes.Hidden);
                    string labelsFilePath = System.IO.Path.Combine(settings.CurrentProjectPath, "class_labels.json");
                    File.SetAttributes(labelsFilePath, FileAttributes.Normal);
                    var labels = LoadClassLabels(labelsFilePath);
                    ViewModel.ClassLabels.Clear();
                    foreach (var label in labels)
                    {
                        ViewModel.ClassLabels.Add(label);
                    }
                    File.SetAttributes(labelsFilePath, FileAttributes.Hidden);
                }
            }
        }
        private void MenuItemNew_Click(object sender, RoutedEventArgs e)
        {
            var binding = BindingOperations.GetBindingExpression(inputTextBox, System.Windows.Controls.TextBox.TextProperty);
            binding?.UpdateSource();

            this.AddClass.IsEnabled = true;
            this.AddClass.Opacity = 1.0;
            this.AddClass.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF007ACC"));

            if (string.IsNullOrWhiteSpace(ViewModel.TitleOfProject))
            {
                System.Windows.MessageBox.Show("Please enter a project name");
                return;
            }

            var settings = SettingsManager.Load();
            string basePath = settings.SaveFolderPath;
            string suffix = GetAnnotationSuffix();

            if (string.IsNullOrEmpty(suffix))
            {
                System.Windows.MessageBox.Show("You did not select a project type");
                return;
            }

            string projectPath = System.IO.Path.Combine(basePath, ViewModel.TitleOfProject + suffix);
            ViewModel.projectWithSuffix = ViewModel.TitleOfProject + suffix;
            if (Directory.Exists(projectPath))
            {
                System.Windows.MessageBox.Show("A project with this name already exists");
                return;
            }

            settings.CurrentProjectPath = projectPath;
            SettingsManager.Save(settings);
            if (suffix == "_Classify")
            {
                ProjectService.CreateClassifyProjectStructure(projectPath);
                this.SaveButton.IsEnabled = false;
                this.SaveButton.Opacity = 0.5;
                this.SaveButton.Background = new SolidColorBrush(Colors.Red);
            }
            else
            {
                if (suffix == "_Pose")
                {
                    this.AddClass.IsEnabled = false;
                    this.AddClass.Opacity = 0.5;
                    this.AddClass.Background = new SolidColorBrush(Colors.Red);
                }

                ProjectService.CreateProjectStructure(
                    projectPath,
                    suffix,
                    ViewModel.SelectedAnnotationType,
                    ViewModel.projectWithSuffix
                );

                this.SaveButton.IsEnabled = true;
                this.SaveButton.Opacity = 1.0;
                this.SaveButton.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF007ACC"));
            }
            Image1.Source = null;
            DrawCanvas.Children.Clear();
            ViewModel.TrainImages.Clear();
            ViewModel.ValImages.Clear();
            ViewModel.TestImages.Clear();
            ViewModel.drawnRectangles.Clear();
            ViewModel.ClassLabels.Clear();
            ViewModel.SelectedImagePath = null;
            ViewModel.SelectedImageName = null;
            ViewModel.PoseKeypoints.Clear();
            ViewModel.PoseSkeletonLines.Clear();
            ViewModel.PosePoints.Clear();
            ViewModel.AllPosePoints.Clear();
            ViewModel.CurrentLabelColor = System.Windows.Media.Brushes.White;
            ViewModel.CurrentProject = ViewModel.TitleOfProject;
            ViewModel.LabelText = settings.SaveFolderPath;
            TrainHeader.IsExpanded = false;
            ValHeader.IsExpanded = false;
            TestHeader.IsExpanded = false;

            if (suffix != "_Classify")
            {
                string colorsJson = Path.Combine(settings.CurrentProjectPath, "colors.json");
                string labelsJson = Path.Combine(settings.CurrentProjectPath, "class_labels.json");
                using (File.Create(colorsJson)) { }
                Thread.Sleep(100);
                File.SetAttributes(colorsJson, FileAttributes.Hidden);
                using (File.Create(labelsJson)) { }
                Thread.Sleep(100);
                File.SetAttributes(labelsJson, FileAttributes.Hidden);
            }
            if(suffix == "_Pose")
            {
                ViewModel.pointCounterText = $"Point {ViewModel.PoseCurrentPointIndex + 1}/17: Nose";
                ViewModel.ClassLabels.Add("person");
                ViewModel.SelectedClassLabel = "person";
            }
            System.Windows.MessageBox.Show("The project has been successfully created!");
        }
        private string GetAnnotationSuffix()
        {
            if (ViewModel.IsOptionDetectChecked) return "_Detect";
            if (ViewModel.IsOptionOBBChecked) return "_OBB";
            if (ViewModel.IsOptionSegmentChecked) return "_Segment";
            if (ViewModel.IsOptionClassifyChecked) return "_Classify";
            if (ViewModel.IsOptionPoseChecked) return "_Pose";
            return null;
        }
        public static bool EndsWithDetect(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return input.EndsWith("_Detect", StringComparison.OrdinalIgnoreCase);
        }
        public static bool EndsWithOBB(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return input.EndsWith("_OBB", StringComparison.OrdinalIgnoreCase);
        }
        public static bool EndsWithSegment(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return input.EndsWith("_Segment", StringComparison.OrdinalIgnoreCase);
        }
        public static bool EndsWithClassify(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return input.EndsWith("_Classify", StringComparison.OrdinalIgnoreCase);
        }
        public static bool EndsWithPose(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return input.EndsWith("_Pose", StringComparison.OrdinalIgnoreCase);
        }
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CurrentProject))
            {
                if (!string.IsNullOrEmpty(ViewModel.CurrentProject))
                {
                    addTrain.IsEnabled = true;
                    addVal.IsEnabled = true;
                    addTest.IsEnabled = true;
                }
            }
            if (e.PropertyName == nameof(ViewModel.SelectedClassLabel))
            {
                if(ViewModel.SelectedAnnotationType == AnnotationType.Classify)
                {
                    return;
                }
                if (ViewModel.SelectedAnnotationType == AnnotationType.Pose)
                {
                    return;
                }
                ViewModel.CurrentLabelColor = ViewModel.ClassColors[ViewModel.SelectedClassLabel];
            }
        }
        public void LoadProjectImages(string projectPath)
        {
            ViewModel.TrainImages.Clear();
            ViewModel.ValImages.Clear();
            ViewModel.TestImages.Clear();

            var trainPath = System.IO.Path.Combine(projectPath, "images", "train");
            var valPath = System.IO.Path.Combine(projectPath, "images", "val");
            var testPath = System.IO.Path.Combine(projectPath, "images", "test");

            if (Directory.Exists(trainPath))
            {
                foreach (var file in Directory.GetFiles(trainPath))
                {
                    ViewModel.TrainImages.Add(System.IO.Path.GetFileName(file));
                }
            }

            if (Directory.Exists(valPath))
            {
                foreach (var file in Directory.GetFiles(valPath))
                {
                    ViewModel.ValImages.Add(System.IO.Path.GetFileName(file));
                }
            }

            if (Directory.Exists(testPath))
            {
                foreach (var file in Directory.GetFiles(testPath))
                {
                    ViewModel.TestImages.Add(System.IO.Path.GetFileName(file));
                }
            }
        }
        private void AddTrainButton_Click(object sender, RoutedEventArgs e)
        {
            if(ViewModel.SelectedAnnotationType == AnnotationType.Classify)
            {
                addTrainClassify();
                return;
            }
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
            dialog.Multiselect = true;
            var settings = SettingsManager.Load();
            if (dialog.ShowDialog() == true)
            {
                foreach (var filename in dialog.FileNames)
                {
                    try
                    {
                        string targetDir = Path.Combine(settings.CurrentProjectPath, "images", "train");
                        if (!Directory.Exists(targetDir))
                            Directory.CreateDirectory(targetDir);

                        string destFileName = Path.Combine(targetDir, Path.GetFileName(filename));

                        File.Copy(filename, destFileName, overwrite: true);

                        ViewModel.TrainImages.Add(Path.GetFileName(destFileName));

                        string imagePath = targetDir;
                        string images = "images";
                        string labels = "labels";
                        string targetLabelPath = imagePath.Replace(images, labels);
                        if (!Directory.Exists(targetLabelPath))
                            Directory.CreateDirectory(targetLabelPath);

                        string checkTxt = Path.Combine(targetLabelPath, Path.ChangeExtension(Path.GetFileName(filename), ".txt"));
                        if (!File.Exists(checkTxt))
                        {
                            File.WriteAllText(checkTxt, string.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void addVal_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedAnnotationType == AnnotationType.Classify)
            {
                addValClassify();
                return;
            }
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
            dialog.Multiselect = true;
            var settings = SettingsManager.Load();


            if (dialog.ShowDialog() == true)
            {
                foreach (var filename in dialog.FileNames)
                {
                    try
                    {
                        string targetDir = Path.Combine(settings.CurrentProjectPath, "images", "val");
                        if (!Directory.Exists(targetDir))
                            Directory.CreateDirectory(targetDir);

                        string destFileName = Path.Combine(targetDir, Path.GetFileName(filename));

                        File.Copy(filename, destFileName, overwrite: true); 

                        ViewModel.ValImages.Add(Path.GetFileName(destFileName));

                        string imagePath = targetDir;
                        string images = "images";
                        string labels = "labels";
                        string targetLabelPath = imagePath.Replace(images, labels);
                        if (!Directory.Exists(targetLabelPath))
                            Directory.CreateDirectory(targetLabelPath);

                        string checkTxt = Path.Combine(targetLabelPath, Path.ChangeExtension(Path.GetFileName(filename), ".txt"));
                        if (!File.Exists(checkTxt))
                        {
                            File.WriteAllText(checkTxt, string.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void addTest_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedAnnotationType == AnnotationType.Classify)
            {
                addTestClassify();
                return;
            }
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
            dialog.Multiselect = true;
            var settings = SettingsManager.Load();

            if (dialog.ShowDialog() == true)
            {
                foreach (var filename in dialog.FileNames)
                {
                    try
                    {
                        string targetDir = Path.Combine(settings.CurrentProjectPath, "images", "test");
                        if (!Directory.Exists(targetDir))
                            Directory.CreateDirectory(targetDir);

                        string destFileName = Path.Combine(targetDir, Path.GetFileName(filename));

                        File.Copy(filename, destFileName, overwrite: true);

                        ViewModel.TestImages.Add(Path.GetFileName(destFileName));

                        string imagePath = targetDir;
                        string imageName = destFileName;
                        string images = "images";
                        string labels = "labels";
                        string targetLabelPath = imagePath.Replace(images, labels);
                        string checkTxt = System.IO.Path.Combine(targetLabelPath, System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(filename), ".txt"));
                        if (!File.Exists(checkTxt))
                        {
                            Directory.CreateDirectory(targetLabelPath);
                            File.WriteAllText(checkTxt, string.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void addTrainClassify()
        {
            if(ViewModel.SelectedClassLabel == null || string.IsNullOrEmpty(ViewModel.SelectedClassLabel))
            {
                MessageBox.Show("Class for adding a photo is not selected");
                return;
            }
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
            dialog.Multiselect = true;
            var settings = SettingsManager.Load();
            if (dialog.ShowDialog() == true)
            {
                foreach (var filename in dialog.FileNames)
                {
                    try
                    {
                        string targetDir = Path.Combine(settings.CurrentProjectPath, "train", ViewModel.SelectedClassLabel);
                        if (!Directory.Exists(targetDir))
                            Directory.CreateDirectory(targetDir);

                        string destFileName = Path.Combine(targetDir, Path.GetFileName(filename));

                        File.Copy(filename, destFileName, overwrite: true); // просто копіюємо файл як є

                        // Додаємо в список у ViewModel
                        ViewModel.TrainImages.Add(Path.GetFileName(destFileName));

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void addValClassify()
        {
            if (ViewModel.SelectedClassLabel == null || string.IsNullOrEmpty(ViewModel.SelectedClassLabel))
            {
                MessageBox.Show("Class for adding a photo is not selected");
                return;
            }
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
            dialog.Multiselect = true;
            var settings = SettingsManager.Load();
            if (dialog.ShowDialog() == true)
            {
                foreach (var filename in dialog.FileNames)
                {
                    try
                    {
                        string targetDir = Path.Combine(settings.CurrentProjectPath, "val", ViewModel.SelectedClassLabel);
                        if (!Directory.Exists(targetDir))
                            Directory.CreateDirectory(targetDir);

                        string destFileName = Path.Combine(targetDir, Path.GetFileName(filename));

                        File.Copy(filename, destFileName, overwrite: true); 

                        ViewModel.ValImages.Add(Path.GetFileName(destFileName));

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void addTestClassify()
        {
            if (ViewModel.SelectedClassLabel == null || string.IsNullOrEmpty(ViewModel.SelectedClassLabel))
            {
                MessageBox.Show("Class for adding a photo is not selected");
                return;
            }
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
            dialog.Multiselect = true;
            var settings = SettingsManager.Load();
            if (dialog.ShowDialog() == true)
            {
                foreach (var filename in dialog.FileNames)
                {
                    try
                    {
                        string targetDir = Path.Combine(settings.CurrentProjectPath, "test", ViewModel.SelectedClassLabel);
                        if (!Directory.Exists(targetDir))
                            Directory.CreateDirectory(targetDir);

                        string destFileName = Path.Combine(targetDir, Path.GetFileName(filename));

                        File.Copy(filename, destFileName, overwrite: true); // просто копіюємо файл як є

                        // Додаємо в список у ViewModel
                        ViewModel.TestImages.Add(Path.GetFileName(destFileName));

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void TrainListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var settings = SettingsManager.Load();
            if(ViewModel.SelectedAnnotationType == AnnotationType.Classify)
            {
                TrainListBox_ClassifyChanged();
                return;
            }

            if (ViewModel.SelectedTrainImage != null)
            {
                TestListBox.SelectedIndex = -1;
                ValListBox.SelectedIndex = -1;
                ViewModel.SelectedImagePath = System.IO.Path.Combine(
                    settings.CurrentProjectPath,
                    "images", "train",
                    ViewModel.SelectedTrainImage
                );

                DrawCanvas.Children.Clear();
                ViewModel.drawnRectangles.Clear();
                ViewModel.drawnPolygons.Clear();

                if (File.Exists(ViewModel.SelectedImagePath))
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(ViewModel.SelectedImagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        Image1.Source = bitmap;
                        Image1.Dispatcher.InvokeAsync(() =>
                        {
                            if (Image1.Source is BitmapSource bitmap)
                            {
                                double imageAspect = bitmap.PixelWidth / (double)bitmap.PixelHeight;
                                double controlAspect = Image1.ActualWidth / Image1.ActualHeight;

                                double displayedWidth, displayedHeight;

                                if (imageAspect > controlAspect)
                                {
                                    displayedWidth = Image1.ActualWidth;
                                    displayedHeight = Image1.ActualWidth / imageAspect;
                                }
                                else
                                {
                                    displayedHeight = Image1.ActualHeight;
                                    displayedWidth = Image1.ActualHeight * imageAspect;
                                }

                                DrawCanvas.Width = displayedWidth;
                                DrawCanvas.Height = displayedHeight;

                                string labelsPath = Path.Combine(
                                    settings.CurrentProjectPath,
                                    "labels", "train",
                                    Path.ChangeExtension(ViewModel.SelectedTrainImage, ".txt")
                                );
                                LoadAnnotations(labelsPath);
                            }
                        }, DispatcherPriority.ContextIdle);

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void TrainListBox_ClassifyChanged()
        {

            var settings = SettingsManager.Load();
            if (ViewModel.SelectedTrainImage != null && ViewModel.SelectedClassLabel != null)
            {
                TestListBox.SelectedIndex = -1;
                ValListBox.SelectedIndex = -1;
                ViewModel.SelectedImagePath = System.IO.Path.Combine(
                    settings.CurrentProjectPath,
                    "train", ViewModel.SelectedClassLabel,
                    ViewModel.SelectedTrainImage
                );

                DrawCanvas.Children.Clear();
                ViewModel.drawnRectangles.Clear();
                ViewModel.drawnPolygons.Clear();

                if (File.Exists(ViewModel.SelectedImagePath))
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(ViewModel.SelectedImagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        Image1.Source = bitmap;
                        Image1.Dispatcher.InvokeAsync(() =>
                        {
                            if (Image1.Source is BitmapSource bitmap)
                            {
                                double imageAspect = bitmap.PixelWidth / (double)bitmap.PixelHeight;
                                double controlAspect = Image1.ActualWidth / Image1.ActualHeight;

                                double displayedWidth, displayedHeight;

                                if (imageAspect > controlAspect)
                                {
                                    displayedWidth = Image1.ActualWidth;
                                    displayedHeight = Image1.ActualWidth / imageAspect;
                                }
                                else
                                {
                                    displayedHeight = Image1.ActualHeight;
                                    displayedWidth = Image1.ActualHeight * imageAspect;
                                }

                                DrawCanvas.Width = displayedWidth;
                                DrawCanvas.Height = displayedHeight;

                            }
                        }, DispatcherPriority.ContextIdle);

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void ValListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var settings = SettingsManager.Load();
            if (ViewModel.SelectedAnnotationType == AnnotationType.Classify)
            {
                ValListBox_ClassifyChanged();
                return;
            }
            if (ViewModel.SelectedValImage != null)
            {
                TrainListBox.SelectedIndex = -1;
                TestListBox.SelectedIndex = -1;
                ViewModel.SelectedImagePath = System.IO.Path.Combine(
                    settings.CurrentProjectPath,
                    "images", "val",
                    ViewModel.SelectedValImage
                );

                DrawCanvas.Children.Clear();
                ViewModel.drawnRectangles.Clear();
                ViewModel.drawnPolygons.Clear();

                if (File.Exists(ViewModel.SelectedImagePath))
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(ViewModel.SelectedImagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        Image1.Source = bitmap;

                        Image1.Dispatcher.InvokeAsync(() =>
                        {
                            if (Image1.Source is BitmapSource bitmap)
                            {
                                double imageAspect = bitmap.PixelWidth / (double)bitmap.PixelHeight;
                                double controlAspect = Image1.ActualWidth / Image1.ActualHeight;

                                double displayedWidth, displayedHeight;

                                if (imageAspect > controlAspect)
                                {
                                    displayedWidth = Image1.ActualWidth;
                                    displayedHeight = Image1.ActualWidth / imageAspect;
                                }
                                else
                                {
                                    displayedHeight = Image1.ActualHeight;
                                    displayedWidth = Image1.ActualHeight * imageAspect;
                                }

                                DrawCanvas.Width = displayedWidth;
                                DrawCanvas.Height = displayedHeight;

                                string labelsPath = Path.Combine(
                                    settings.CurrentProjectPath,
                                    "labels", "val",
                                    Path.ChangeExtension(ViewModel.SelectedValImage, ".txt")
                                );
                                LoadAnnotations(labelsPath);
                            }
                        }, DispatcherPriority.ContextIdle);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void ValListBox_ClassifyChanged()
        {
            var settings = SettingsManager.Load();
            if (ViewModel.SelectedValImage != null && ViewModel.SelectedClassLabel != null)
            {
                TestListBox.SelectedIndex = -1;
                TrainListBox.SelectedIndex = -1;
                ViewModel.SelectedImagePath = System.IO.Path.Combine(
                    settings.CurrentProjectPath,
                    "val", ViewModel.SelectedClassLabel,
                    ViewModel.SelectedValImage
                );

                DrawCanvas.Children.Clear();
                ViewModel.drawnRectangles.Clear();
                ViewModel.drawnPolygons.Clear();

                if (File.Exists(ViewModel.SelectedImagePath))
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(ViewModel.SelectedImagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        Image1.Source = bitmap;
                        Image1.Dispatcher.InvokeAsync(() =>
                        {
                            if (Image1.Source is BitmapSource bitmap)
                            {
                                double imageAspect = bitmap.PixelWidth / (double)bitmap.PixelHeight;
                                double controlAspect = Image1.ActualWidth / Image1.ActualHeight;

                                double displayedWidth, displayedHeight;

                                if (imageAspect > controlAspect)
                                {
                                    displayedWidth = Image1.ActualWidth;
                                    displayedHeight = Image1.ActualWidth / imageAspect;
                                }
                                else
                                {
                                    displayedHeight = Image1.ActualHeight;
                                    displayedWidth = Image1.ActualHeight * imageAspect;
                                }

                                DrawCanvas.Width = displayedWidth;
                                DrawCanvas.Height = displayedHeight;

                            }
                        }, DispatcherPriority.ContextIdle);

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void TestListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var settings = SettingsManager.Load();
            if (ViewModel.SelectedAnnotationType == AnnotationType.Classify)
            {
                TestListBox_ClassifyChanged();
                return;
            }
            if (ViewModel.SelectedTestImage != null)
            {
                TrainListBox.SelectedIndex = -1;
                ValListBox.SelectedIndex = -1;
                ViewModel.SelectedImagePath = System.IO.Path.Combine(
                    settings.CurrentProjectPath,
                    "images", "test",
                    ViewModel.SelectedTestImage
                );

                DrawCanvas.Children.Clear();
                ViewModel.drawnRectangles.Clear();
                ViewModel.drawnPolygons.Clear();

                if (File.Exists(ViewModel.SelectedImagePath))
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(ViewModel.SelectedImagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        Image1.Source = bitmap;

                        Image1.Dispatcher.InvokeAsync(() =>
                        {
                            if (Image1.Source is BitmapSource bitmap)
                            {
                                double imageAspect = bitmap.PixelWidth / (double)bitmap.PixelHeight;
                                double controlAspect = Image1.ActualWidth / Image1.ActualHeight;

                                double displayedWidth, displayedHeight;

                                if (imageAspect > controlAspect)
                                {
                                    displayedWidth = Image1.ActualWidth;
                                    displayedHeight = Image1.ActualWidth / imageAspect;
                                }
                                else
                                {
                                    displayedHeight = Image1.ActualHeight;
                                    displayedWidth = Image1.ActualHeight * imageAspect;
                                }

                                DrawCanvas.Width = displayedWidth;
                                DrawCanvas.Height = displayedHeight;

                                // Завантаження анотацій після коректного масштабування
                                string labelsPath = Path.Combine(
                                    settings.CurrentProjectPath,
                                    "labels", "test",
                                    Path.ChangeExtension(ViewModel.SelectedTestImage, ".txt")
                                );
                                LoadAnnotations(labelsPath);
                            }
                        }, DispatcherPriority.ContextIdle);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void TestListBox_ClassifyChanged()
        {
            var settings = SettingsManager.Load();
            if (ViewModel.SelectedTestImage != null && ViewModel.SelectedClassLabel != null)
            {
                TrainListBox.SelectedIndex = -1;
                ValListBox.SelectedIndex = -1;
                ViewModel.SelectedImagePath = System.IO.Path.Combine(
                    settings.CurrentProjectPath,
                    "test", ViewModel.SelectedClassLabel,
                    ViewModel.SelectedTestImage
                );

                DrawCanvas.Children.Clear();
                ViewModel.drawnRectangles.Clear();
                ViewModel.drawnPolygons.Clear();

                if (File.Exists(ViewModel.SelectedImagePath))
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(ViewModel.SelectedImagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        Image1.Source = bitmap;
                        Image1.Dispatcher.InvokeAsync(() =>
                        {
                            if (Image1.Source is BitmapSource bitmap)
                            {
                                double imageAspect = bitmap.PixelWidth / (double)bitmap.PixelHeight;
                                double controlAspect = Image1.ActualWidth / Image1.ActualHeight;

                                double displayedWidth, displayedHeight;

                                if (imageAspect > controlAspect)
                                {
                                    displayedWidth = Image1.ActualWidth;
                                    displayedHeight = Image1.ActualWidth / imageAspect;
                                }
                                else
                                {
                                    displayedHeight = Image1.ActualHeight;
                                    displayedWidth = Image1.ActualHeight * imageAspect;
                                }

                                DrawCanvas.Width = displayedWidth;
                                DrawCanvas.Height = displayedHeight;

                            }
                        }, DispatcherPriority.ContextIdle);

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while processing the image: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.Focus();

            if (ViewModel.SelectedClassLabel == null)
            {
                MessageBox.Show("Оберіть клас для розмітки");
                return;
            }

            UpdateDrawer();
            currentDrawer?.OnMouseDown(e.GetPosition(DrawCanvas));
        }
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point position = e.GetPosition(OverlayCanvas);
            ShowCrosshair(position);

            currentDrawer?.OnMouseMove(e.GetPosition(DrawCanvas));
        }
        private void DrawCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            ClearCrosshair(); 
        }
        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            currentDrawer?.OnMouseUp(e.GetPosition(DrawCanvas));
            ViewModel.saveStatus = "\u2B55";
            ClearCrosshair(); 
        }
        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                UndoLastRectangle();
                ViewModel.saveStatus = "\u2B55";
            }

        }
        private Line verticalLine;
        private Line horizontalLine;
        private TextBlock plusCursor;
        private void ShowCrosshair(Point position)
        {
            DrawCanvas.Cursor = Cursors.None;
            double width = OverlayCanvas.ActualWidth;
            double height = OverlayCanvas.ActualHeight;

            if (verticalLine == null)
            {
                verticalLine = new Line
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                OverlayCanvas.Children.Add(verticalLine);
            }

            if (horizontalLine == null)
            {
                horizontalLine = new Line
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                OverlayCanvas.Children.Add(horizontalLine);
            }

            if (plusCursor == null)
            {
                plusCursor = new TextBlock
                {
                    Text = "+",
                    FontSize = 42,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Red,
                    IsHitTestVisible = false
                };
                OverlayCanvas.Children.Add(plusCursor);
            }

            verticalLine.X1 = position.X;
            verticalLine.Y1 = 0;
            verticalLine.X2 = position.X;
            verticalLine.Y2 = height;

            horizontalLine.X1 = 0;
            horizontalLine.Y1 = position.Y;
            horizontalLine.X2 = width;
            horizontalLine.Y2 = position.Y;

            Canvas.SetLeft(plusCursor, position.X - 15); 
            Canvas.SetTop(plusCursor, position.Y - 33);
        }
        private void ClearCrosshair()
        {
            OverlayCanvas.Children.Clear();
            verticalLine = null;
            horizontalLine = null;
            plusCursor = null;
        }
        private void UndoLastRectangle()
        {
            var allElements = new List<UIElement>();
            allElements.AddRange(ViewModel.drawnPolygons);
            allElements.AddRange(ViewModel.drawnRectangles);

            UIElement lastElement = allElements.LastOrDefault();
            if (lastElement != null)
            {
                DrawCanvas.Children.Remove(lastElement);

                if (lastElement is Rectangle rect)
                    ViewModel.drawnRectangles.Remove(rect);
                else if (lastElement is Polygon poly)
                    ViewModel.drawnPolygons.Remove(poly);
            }
            else if (ViewModel.Poses.Count > 0)
            {
                var lastPose = ViewModel.Poses.Last();
                if (lastPose.PoseGroup != null)
                {
                    DrawCanvas.Children.Remove(lastPose.PoseGroup);
                }

                ViewModel.Poses.Remove(lastPose);
            }
        }
        private void ChooseProjectsPath(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Load();
            var path = SettingsManager.ChooseSaveFolderPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                settings.SaveFolderPath = path;
                settings.CurrentProjectPath = null; 
                SettingsManager.Save(settings);
                ViewModel.LabelText = $"{path}";
            }
        }
        private void LoadAnnotations(string labelPath)
        {
            if (!File.Exists(labelPath) || Image1.Source == null)
                return;

            DrawCanvas.Children.Clear();
            ViewModel.drawnRectangles.Clear();
            ViewModel.drawnPolygons.Clear();
            ViewModel.drawnPoses.Clear();
            ViewModel.PoseKeypoints.Clear();
            ViewModel.PoseSkeletonLines.Clear();
            ViewModel.PosePoints.Clear();
            ViewModel.AllPosePoints.Clear();
            double canvasWidth = DrawCanvas.Width;
            double canvasHeight = DrawCanvas.Height;

            var lines = File.ReadAllLines(labelPath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(' ');
                if (parts.Length < 5) continue;

                int classIndex = int.Parse(parts[0]);
                string classLabel = (ViewModel.ClassLabels.Count > classIndex && classIndex >= 0)
                    ? ViewModel.ClassLabels[classIndex]
                    : "Unknown";

                var stroke = ViewModel.ClassColors.ContainsKey(classLabel)
                    ? ViewModel.ClassColors[classLabel]
                    : Brushes.Red;

                // BBox
                if (ViewModel.projectWithSuffix.EndsWith("_Detect", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts.Length != 5) continue;

                    double normCenterX = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    double normCenterY = double.Parse(parts[2], CultureInfo.InvariantCulture);
                    double normWidth = double.Parse(parts[3], CultureInfo.InvariantCulture);
                    double normHeight = double.Parse(parts[4], CultureInfo.InvariantCulture);

                    double rectWidth = normWidth * canvasWidth;
                    double rectHeight = normHeight * canvasHeight;
                    double centerX = normCenterX * canvasWidth;
                    double centerY = normCenterY * canvasHeight;

                    double left = centerX - rectWidth / 2;
                    double top = centerY - rectHeight / 2;

                    var rect = new Rectangle
                    {
                        Width = rectWidth,
                        Height = rectHeight,
                        StrokeThickness = 2,
                        Stroke = stroke,
                        Tag = classLabel
                    };

                    Canvas.SetLeft(rect, left);
                    Canvas.SetTop(rect, top);
                    DrawCanvas.Children.Add(rect);
                    ViewModel.drawnRectangles.Add(rect);
                }
                // OBB 
                else if (ViewModel.projectWithSuffix.EndsWith("_OBB", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts.Length != 9) continue;

                    PointCollection points = new PointCollection();
                    for (int i = 1; i < 9; i += 2)
                    {
                        double normX = double.Parse(parts[i], CultureInfo.InvariantCulture);
                        double normY = double.Parse(parts[i + 1], CultureInfo.InvariantCulture);

                        double canvasX = normX * canvasWidth;
                        double canvasY = normY * canvasHeight;

                        points.Add(new Point(canvasX, canvasY));
                    }

                    Polygon polygon = new Polygon
                    {
                        Points = points,
                        StrokeThickness = 2,
                        Stroke = stroke,
                        Fill = Brushes.Transparent,
                        Tag = classLabel
                    };

                    DrawCanvas.Children.Add(polygon);
                    ViewModel.drawnPolygons.Add(polygon);
                }
                // Segment
                else if (ViewModel.projectWithSuffix.EndsWith("_Segment", StringComparison.OrdinalIgnoreCase))
                {
                    PointCollection points = new PointCollection();

                    for (int i = 1; i < parts.Length - 1; i += 2)
                    {
                        double normX = double.Parse(parts[i], CultureInfo.InvariantCulture);
                        double normY = double.Parse(parts[i + 1], CultureInfo.InvariantCulture);

                        double x = normX * canvasWidth;
                        double y = normY * canvasHeight;

                        points.Add(new Point(x, y));
                    }
                    var fill = new SolidColorBrush(((SolidColorBrush)stroke).Color) { Opacity = 0.5 };
                    var polygon = new Polygon
                    {
                        Points = points,
                        Stroke = stroke,
                        StrokeThickness = 2,
                        Fill = fill,
                        Tag = classLabel
                    };

                    DrawCanvas.Children.Add(polygon);
                    ViewModel.drawnPolygons.Add(polygon);
                }
                // Pose
                else if (ViewModel.projectWithSuffix.EndsWith("_Pose", StringComparison.OrdinalIgnoreCase))
                {
                    var pose = new PoseAnnotation();
                    for (int i = 0; i < 17; i++)
                    {
                        int offset = 5 + i * 3;
                        double px = double.Parse(parts[offset], CultureInfo.InvariantCulture) * canvasWidth;
                        double py = double.Parse(parts[offset + 1], CultureInfo.InvariantCulture) * canvasHeight;
                        int visibility = int.Parse(parts[offset + 2]);

                        pose.RawPoints.Add(new Point(px, py));

                        var kp = new Ellipse
                        {
                            Width = 6,
                            Height = 6,
                            Fill = visibility == 0 ? Brushes.Gray : Brushes.Yellow,
                            Stroke = Brushes.Black,
                            StrokeThickness = 1,
                            Tag = visibility
                        };

                        Canvas.SetLeft(kp, px - 3);
                        Canvas.SetTop(kp, py - 3);

                        pose.Keypoints.Add(kp);
                    }

                    var skeleton = GetSkeletonConnections();
                    foreach (var (i1, i2) in skeleton)
                    {
                        if (i1 >= pose.RawPoints.Count || i2 >= pose.RawPoints.Count)
                            continue;

                        var line2 = new Line
                        {
                            X1 = pose.RawPoints[i1].X,
                            Y1 = pose.RawPoints[i1].Y,
                            X2 = pose.RawPoints[i2].X,
                            Y2 = pose.RawPoints[i2].Y,
                            Stroke = Brushes.Orange,
                            StrokeThickness = 2
                        };

                        pose.SkeletonLines.Add(line2);
                    }

                    foreach (var kp in pose.Keypoints)
                        pose.PoseGroup.Children.Add(kp);
                    foreach (var ln in pose.SkeletonLines)
                        pose.PoseGroup.Children.Add(ln);

                    DrawCanvas.Children.Add(pose.PoseGroup);

                    ViewModel.Poses.Add(pose);

                }
            }
        }
        public class PoseAnnotation
        {
            public List<Ellipse> Keypoints { get; set; } = new();
            public List<Line> SkeletonLines { get; set; } = new();
            public List<Point> RawPoints { get; set; } = new();
            public Canvas PoseGroup { get; set; } = new();
        }
        public static List<(int, int)> GetSkeletonConnections()
        {
            return new List<(int, int)>
        {
            (0, 1), // nose - left eye
            (0, 2), // nose - right eye
            (1, 3), // left eye - left ear
            (2, 4), // right eye - right ear
            (0, 5), // nose - left shoulder
            (0, 6), // nose - right shoulder
            (5, 7), // left shoulder - left elbow
            (7, 9), // left elbow - left wrist
            (6, 8), // right shoulder - right elbow
            (8, 10), // right elbow - right wrist
            (5, 11), // left shoulder - left hip
            (6, 12), // right shoulder - right hip
            (11, 13), // left hip - left knee
            (13, 15), // left knee - left ankle
            (12, 14), // right hip - right knee
            (14, 16), // right knee - right ankle
            (11, 12) // left hip - right hip
        };
        }
        private void SaveClasses()
        {
            var settings = SettingsManager.Load();
            string labelsFileName = ViewModel.projectWithSuffix + ".yaml";
            string labelPath = System.IO.Path.Combine(settings.CurrentProjectPath, labelsFileName);

            using (StreamWriter writer = new StreamWriter(labelPath))
            {
                int index = 0;
                string checkImgTest = Path.Combine(settings.CurrentProjectPath, "images", "test");
                string checkLabTest = Path.Combine(settings.CurrentProjectPath, "labels", "test");
                string testLineInYaml;
                if (Directory.Exists(checkImgTest) && Directory.Exists(checkLabTest)) {
                    testLineInYaml = "train: images/train\nval: images/val\ntest: images/test\n\nnames:";
                }
                else
                {
                    testLineInYaml = "train: images/train\nval: images/val\n\nnames:";
                }
                   
                writer.WriteLine(testLineInYaml);
                foreach (var classLabel in ViewModel.ClassLabels)
                {
                    writer.WriteLine("  " + index + ": " + classLabel);
                    index++;
                }
            }
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string imagePath = ViewModel.SelectedImagePath;
            string images = "images";
            string labels = "labels";
            string labelsPath;
            if (imagePath == null)
            {
                return;
            }
            else
            {
                labelsPath = imagePath.Replace(images, labels);
            }

            var settings = SettingsManager.Load();

            if (ViewModel.SelectedAnnotationType == AnnotationType.Detect)
            {
                YoloAnnotationTool2.Services.AnnotationService.SaveAnnotations(
                    labelsPath,
                    ViewModel.drawnRectangles.Cast<UIElement>().ToList(),
                    ViewModel.SelectedAnnotationType,
                    ViewModel.ClassColors,
                    ViewModel.ClassLabels,
                    DrawCanvas.Width,
                    DrawCanvas.Height
                    );
            }
            else if (ViewModel.SelectedAnnotationType == AnnotationType.OBB)
            {
                var allElements = new List<UIElement>();
                allElements.AddRange(ViewModel.drawnRectangles);
                allElements.AddRange(ViewModel.drawnPolygons);

                YoloAnnotationTool2.Services.AnnotationService.SaveAnnotations(
                    labelsPath,
                    allElements,
                    ViewModel.SelectedAnnotationType,
                    ViewModel.ClassColors,
                    ViewModel.ClassLabels,
                    DrawCanvas.Width,
                    DrawCanvas.Height
                );
            }
            else if (ViewModel.SelectedAnnotationType == AnnotationType.Segment)
            {
                YoloAnnotationTool2.Services.AnnotationService.SaveAnnotations(
                    labelsPath,
                    ViewModel.drawnPolygons.Cast<UIElement>().ToList(),
                    ViewModel.SelectedAnnotationType,
                    ViewModel.ClassColors,
                    ViewModel.ClassLabels,
                    DrawCanvas.Width,
                    DrawCanvas.Height
                    );
            }
            else if (ViewModel.SelectedAnnotationType == AnnotationType.Pose)
            {
                PoseAnnotationService.SavePoseAnnotations(
                    labelsPath,
                    DrawCanvas.Width,
                    DrawCanvas.Height,
                    ViewModel.AllPosePoints,
                    ViewModel.PoseBodyParts
                );
                PoseAnnotationService.ClearPoseData(ViewModel);
                ViewModel.saveStatus = "\u2714";
                return;
            }
            SaveClasses();
            SaveClassBrushes(ViewModel.ClassColors, settings.CurrentProjectPath);
            string labelsFilePath = System.IO.Path.Combine(settings.CurrentProjectPath, "class_labels.json");
            File.SetAttributes(labelsFilePath, FileAttributes.Normal);
            SaveClassLabels(ViewModel.ClassLabels, labelsFilePath);
            File.SetAttributes(labelsFilePath, FileAttributes.Hidden);
            ViewModel.saveStatus = "\u2714";
        }
        private void AddClassButton_Click(object sender, RoutedEventArgs e)
        {
            string newClass = NewClassTextBox.Text.Trim();
            if(ViewModel.SelectedAnnotationType == AnnotationType.Classify)
            {
                if (ViewModel.CurrentProject == null || string.IsNullOrEmpty(ViewModel.CurrentProject))
                {
                    MessageBox.Show("You cannot add a class without a created project");
                    return;
                }
                addClassClassify(newClass);
                return;
            }
            if(ViewModel.CurrentProject == null || string.IsNullOrEmpty(ViewModel.CurrentProject))
            {
                MessageBox.Show("You cannot add a class without a created project");
                return;
            }
            if (!string.IsNullOrEmpty(newClass) && !ViewModel.ClassLabels.Contains(newClass))
            {
                // Додаємо новий клас у список
                ViewModel.ClassLabels.Add(newClass);

                // Генеруємо випадковий колір для нового класу
                System.Windows.Media.Brush randomColor = ViewModel.GetRandomColor();

                // Зберігаємо колір для цього класу
                ViewModel.ClassColors[newClass] = randomColor;

                // Вибираємо цей клас як поточний
                ViewModel.SelectedClassLabel = newClass;

                NewClassTextBox.Clear();
            }
            ViewModel.saveStatus = "\u2B55";
        }
        private void addClassClassify(string newClass)
        {
            ViewModel.ClassLabels.Add(newClass);
            ViewModel.SelectedClassLabel = newClass;

            NewClassTextBox.Clear();
            var settings = SettingsManager.Load();
            string pathToTrain = Path.Combine(settings.CurrentProjectPath, "train");
            string pathToVal = Path.Combine(settings.CurrentProjectPath, "val");
            string pathToTest = Path.Combine(settings.CurrentProjectPath, "test");
            if (!Directory.Exists(Path.Combine(pathToTrain, newClass)))
            {
                Directory.CreateDirectory(Path.Combine(pathToTrain, newClass));
            }
            if (!Directory.Exists(Path.Combine(pathToVal, newClass)))
            {
                Directory.CreateDirectory(Path.Combine(pathToVal, newClass));
            }
            if (Directory.Exists(pathToTest))
            {
                Directory.CreateDirectory(Path.Combine(pathToTest, newClass));
            }

        }
        public class ClassColor
        {
            public string ClassName { get; set; }
            public string Color { get; set; }  // Зберігаємо як строку
        }
        public void SaveClassBrushes(Dictionary<string, System.Windows.Media.Brush> classBrushes, string filePath)
        {
            filePath = System.IO.Path.Combine(filePath, "colors.json");
            File.SetAttributes(filePath, FileAttributes.Normal);
            var colorStrings = classBrushes.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    if (kvp.Value is SolidColorBrush solidColorBrush)
                    {
                        System.Windows.Media.Color color = solidColorBrush.Color;
                        return $"{color.R},{color.G},{color.B}";
                    }
                    else
                    {
                        return "0,0,0";
                    }
                }
            );

            string json = JsonConvert.SerializeObject(colorStrings, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, json);
            File.SetAttributes(filePath, FileAttributes.Hidden);
        }
        public Dictionary<string, System.Windows.Media.Brush> LoadClassBrushes(string filePath)
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var colorStrings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                var classBrushes = colorStrings.ToDictionary(
                    kvp => kvp.Key,
                    kvp =>
                    {
                        var parts = kvp.Value.Split(',');
                        byte r = byte.Parse(parts[0]);
                        byte g = byte.Parse(parts[1]);
                        byte b = byte.Parse(parts[2]);
                        return (System.Windows.Media.Brush)new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
                    }
                );

                return classBrushes;
            }
            else
            {
                return new Dictionary<string, System.Windows.Media.Brush>();
            }
        }
        private void SaveClassLabels(ObservableCollection<string> classLabels, string filePath)
        {
            string json = JsonConvert.SerializeObject(classLabels, Newtonsoft.Json.Formatting.Indented);
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.WriteAllText(filePath, json);
            File.SetAttributes(filePath, FileAttributes.Hidden);
        }
        private ObservableCollection<string> LoadClassLabels(string filePath)
        {
            if (!File.Exists(filePath))
                return new ObservableCollection<string>();

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ObservableCollection<string>>(json);
        }
        private IAnnotationDrawer currentDrawer;
        private void UpdateDrawer()
        {
            AnnotationType at = AnnotationType.Empty;
            if (ViewModel.projectWithSuffix.EndsWith("_Detect", StringComparison.OrdinalIgnoreCase)) {
                at = AnnotationType.Detect;
            }
            else if (ViewModel.projectWithSuffix.EndsWith("_OBB", StringComparison.OrdinalIgnoreCase))
            {
                at = AnnotationType.OBB;
            }
            else if (ViewModel.projectWithSuffix.EndsWith("_Segment", StringComparison.OrdinalIgnoreCase))
            {
                at = AnnotationType.Segment;
            }
            else if (ViewModel.projectWithSuffix.EndsWith("_Pose", StringComparison.OrdinalIgnoreCase))
            {
                at = AnnotationType.Pose;

            }
            currentDrawer = AnnotationDrawerFactory.CreateDrawer(
                    at,
                    ViewModel,
                    DrawCanvas,
                    Image1
                );
        }
        private void OBB_Checked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedAnnotationType = AnnotationType.OBB;
        }
        private void Detect_Checked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedAnnotationType = AnnotationType.Detect;
        }
        private void Segment_Checked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedAnnotationType = AnnotationType.Segment;
        }
        private void Classify_Checked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedAnnotationType = AnnotationType.Classify;
        }
        private void Pose_Checked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedAnnotationType = AnnotationType.Pose;
        }
        private void SaveZipClick(object sender, RoutedEventArgs e)
        {
            var setting = SettingsManager.Load();
            string directoryPath = setting.CurrentProjectPath;
            string directoryName = new DirectoryInfo(directoryPath).Name;

            string zipFilePath = System.IO.Path.Combine(Directory.GetParent(directoryPath).FullName, directoryName + ".zip");

            ZipService.CreateZipArchive(
                directoryPath,
                ViewModel.projectWithSuffix,
                ViewModel.SelectedAnnotationType
                );
        }
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if(ViewModel.SelectedAnnotationType != AnnotationType.Classify)
            {
                return;
            }
            ViewModel.TrainImages.Clear();
            ViewModel.ValImages.Clear();
            ViewModel.TestImages.Clear();

            if(ViewModel.SelectedClassLabel == null)
            {
                return;
            }
            else
            {
                LoadImagesSelectedClass();
            }
                
        }
        private void LoadImagesSelectedClass()
        {
            var settings = SettingsManager.Load();
            string folderPath = Path.Combine(settings.CurrentProjectPath, "train", ViewModel.SelectedClassLabel);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            string[] fileNames = Directory.GetFiles(folderPath)
                                          .Select(f => Path.GetFileName(f))
                                          .ToArray();
            foreach (string file in fileNames) {
                ViewModel.TrainImages.Add(file);
            }
            string folderPathVal = Path.Combine(settings.CurrentProjectPath, "val", ViewModel.SelectedClassLabel);
            if (!Directory.Exists(folderPathVal))
            {
                Directory.CreateDirectory(folderPathVal);
            }
            string[] fileNamesVal = Directory.GetFiles(folderPathVal)
                                          .Select(f => Path.GetFileName(f))
                                          .ToArray();
            foreach (string file2 in fileNamesVal)
            {
                ViewModel.ValImages.Add(file2);
            }
            string folderPathTest = Path.Combine(settings.CurrentProjectPath, "test", ViewModel.SelectedClassLabel);
            if (!Directory.Exists(folderPathTest))
            {
                return;
            }
            string[] fileNamesTest = Directory.GetFiles(folderPathTest)
                                          .Select(f => Path.GetFileName(f))
                                          .ToArray();
            foreach (string file in fileNamesTest)
            {
                ViewModel.TestImages.Add(file);
            }
        }
    }
}
