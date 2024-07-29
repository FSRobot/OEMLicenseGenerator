// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using CommonModel;
using GroupDocs.Parser;
using GroupDocs.Parser.Data;
using Microsoft.Win32;
using QRCoder;
using System.IO;
using CommonModel.Encryption;
using LicenseChecker;
using Wpf.Ui;
using Wpf.Ui.Controls;
using License = CommonModel.License;
using System.Diagnostics;
using Newtonsoft.Json;

namespace OEMLicenseGenerator.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private ISnackbarService snackbarService { get; }
        private LicenseHelper Helper { get; }
        private LicenseCode _licenseCode;
        public DashboardViewModel(ISnackbarService snackbar, LicenseHelper helper)
        {
            snackbarService = snackbar;
            this.Helper = helper;
        }

        [ObservableProperty] private string _machineCode;
        [ObservableProperty] private DateTime _expireDate = DateTime.Now;
        [ObservableProperty] private bool _unLock;

        [RelayCommand]
        public void SetMouth(object obj)
        {
            if (obj is not string str) return;
            if (!int.TryParse(str, out int mouth)) return;

            ExpireDate = DateTime.Now.AddMonths(mouth);
        }

        [RelayCommand]
        public void GenerateCode()
        {
            if (string.IsNullOrWhiteSpace(MachineCode))
            {
                snackbarService.Show
                (
                    "授权",
                    "序列号禁止为空!",
                    ControlAppearance.Danger,
                    null,
                    TimeSpan.FromSeconds(5)
                );
                return;
            }

            var license = new License()
            {
                ProductName = _licenseCode.ProductName,
                BeginDate = DateTime.Now,
                MachineCode = _licenseCode.Code,
                EndDate = UnLock ? DateTime.MaxValue : ExpireDate
            };
            var code = Helper.Encrypt(license);
            try
            {
                //reg add HKLM\SOFTWARE\WOW6432Node\JKSoft /v product /t REG_SZ /d "激活码"
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var path = $"{desktopPath}\\OEM-License.Key";
                var licenseCode = new LicenseCode()
                {
                    Code = code.ToString(),
                    ProductName = $"{license.ProductName}"
                };
                File.WriteAllText(path, JsonConvert.SerializeObject(licenseCode));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            snackbarService.Show
            (
                "授权",
                "成功!",
                ControlAppearance.Primary,
                null,
                TimeSpan.FromSeconds(5)
            );
        }

        [RelayCommand]
        public void OnImportFile()
        {
            try
            {

                var openFileDialog =
                    new OpenFileDialog()
                    {
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                        Filter = "注册码 (*.*)|*.key;*.txt;*.png;*.jpg",
                        DereferenceLinks = false,
                    };

                if (openFileDialog.ShowDialog() != true || !File.Exists(openFileDialog.FileName))
                {
                    return;
                }

                var extension = Path.GetExtension(openFileDialog.FileName);
                if (extension.Equals(".png") || extension.Equals(".jpg"))
                {
                    var data = ScanQR(openFileDialog.FileName);
                    if (data == null)
                    {
                        snackbarService.Show
                        (
                            "",
                            $"二维码扫描失败!",
                            ControlAppearance.Danger,
                            new SymbolIcon(SymbolRegular.ErrorCircle24),
                            TimeSpan.FromSeconds(5)
                        );
                        return;
                    }
                    _licenseCode =
                        JsonConvert.DeserializeObject<LicenseCode>(data.Value)!;
                    MachineCode = _licenseCode.Code;
                }
                else
                {
                    _licenseCode =
                        JsonConvert.DeserializeObject<LicenseCode>(File.ReadAllText(openFileDialog.FileName))!;
                    MachineCode = _licenseCode.Code;
                }

                snackbarService.Show
                (
                    "",
                    $"导入成功:[{MachineCode}]",
                    ControlAppearance.Info,
                    new SymbolIcon(SymbolRegular.Info24),
                    TimeSpan.FromSeconds(3)
                );
            }
            catch (Exception e)
            {
                snackbarService.Show
                (
                    "",
                    $"导入失败,类型错误!",
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(3)
                );
            }
        }

        [RelayCommand]
        public void OnOpenPicture()
        {
            var openFileDialog =
               new OpenFileDialog()
               {
                   Filter = "Image files (*.bmp;*.jpg;*.jpeg;*.png)|*.bmp;*.jpg;*.jpeg;*.png|All files (*.*)|*.*",
                   DereferenceLinks = false,
               };

            if (openFileDialog.ShowDialog() != true || !File.Exists(openFileDialog.FileName))
            {
                return;
            }

            var data = ScanQR(openFileDialog.FileName);
            snackbarService.Show
            (
                "",
                $"{data.CodeTypeName},{data.Value}",
                ControlAppearance.Info,
                new SymbolIcon(SymbolRegular.Info24),
                TimeSpan.FromSeconds(3)
            );
        }

        public PageBarcodeArea? ScanQR(string filePath)
        {
            var content = string.Empty;
            using Parser parser = new Parser(filePath);
            var code = parser.GetBarcodes().FirstOrDefault();
            return code;
        }

        public string GeneratorQR(string content, string filePath = "")
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Default);
            PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
            if (string.IsNullOrWhiteSpace(filePath))
                filePath = $"{Environment.CurrentDirectory}\\qr.png";
            File.WriteAllBytes(filePath, qrCodeAsPngByteArr);
            return filePath;
        }
    }
}
