//
//  Copyright 2012  Xamarin Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
using System;
using CoreGraphics;
using Foundation;
using UIKit;
using FieldService.Utilities;
using FieldService.ViewModels;
using FieldService.Data;

namespace FieldService.iOS
{
	/// <summary>
	/// Controller for the confirmation screen
	/// </summary>
	public partial class ConfirmationController : BaseController
	{
		readonly AssignmentViewModel assignmentViewModel;
		readonly PhotoViewModel photoViewModel;
		readonly CGSize photoSize = new CGSize (475f, 410f); //Used for desired size of photos
		PhotoAlertSheet photoSheet;

		public ConfirmationController (IntPtr handle) : base (handle)
		{
			assignmentViewModel = ServiceContainer.Resolve<AssignmentViewModel>();
			photoViewModel = ServiceContainer.Resolve<PhotoViewModel>();

			//Update photoSize for screen scale
			var scale = UIScreen.MainScreen.Scale;
			photoSize.Width *= scale;
			photoSize.Height *= scale;
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			//UI setup from code
			View.BackgroundColor = Theme.BackgroundColor;
			photoSheet = new PhotoAlertSheet();
			photoSheet.DesiredSize = photoSize;
			photoSheet.Callback = image => {
				photoViewModel.SelectedPhoto.Image = image.ToByteArray ();

				var addPhotoController = Storyboard.InstantiateViewController<AddPhotoController>();
				addPhotoController.Dismissed += (sender, e) => ReloadConfirmation ();
				PresentViewController(addPhotoController, true, null);
			};
			addPhoto.SetBackgroundImage (Theme.ButtonDark, UIControlState.Normal);
			addPhoto.SetTitleColor (UIColor.White, UIControlState.Normal);

			//Setup our toolbar
			var label = new UILabel (new CGRect (0f, 0f, 120f, 36f)) {
				Text = "Confirmations",
				TextColor = UIColor.White,
				BackgroundColor = UIColor.Clear,
				Font = Theme.BoldFontOfSize (16f),
			};
			var descriptionButton = new UIBarButtonItem (label);
			toolbar.Items = new UIBarButtonItem[] { descriptionButton };

			photoTableView.Source = new PhotoTableSource (this);
			signatureTableView.Source = new SignatureTableSource (this);

			if (!Theme.IsiOS7)
				return;

			photoTableView.RowHeight = 64f;
			addPhoto.AutoresizingMask = UIViewAutoresizing.None;
			addPhoto.SetTitleColor (Theme.LabelColor, UIControlState.Normal);
			addPhoto.SetImage (Theme.ImagePlaceholder, UIControlState.Normal);
			addPhoto.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
			addPhoto.TitleEdgeInsets = new UIEdgeInsets (0f, 18f, 0f, 0f);

			var frame = addPhoto.Frame;
			frame.X = 9f;
			frame.Y += 10f;
			frame.Height = 64f;
			frame.Width = addPhoto.Superview.Frame.Width - 20f;
			addPhoto.Frame = frame;

			frame = addPhoto.Superview.Frame;
			frame.Height = addPhoto.Frame.Bottom;
			addPhoto.Superview.Frame = frame;

			signature.TextColor =
				photos.TextColor =
				requirement.TextColor =
				note.TextColor = Theme.LabelColor;

			addPhoto.Font =
				signature.Font =
				photos.Font = Theme.FontOfSize (18f);
			requirement.Font =
				note.Font = Theme.FontOfSize (12f);
		}

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);

			ReloadConfirmation ();
		}

		/// <summary>
		/// Loads the confirmation screen's info
		/// </summary>
		public void ReloadConfirmation ()
		{
			if (!IsViewLoaded)
				return;
			
			var assignment = assignmentViewModel.SelectedAssignment;
			toolbar.SetBackgroundImage (assignment.IsHistory ? Theme.OrangeBar : Theme.BlueBar, UIToolbarPosition.Any, UIBarMetrics.Default);

			signatureTableView.ReloadData ();

			photoViewModel.LoadPhotosAsync (assignment)
				.ContinueWith (assignmentViewModel.LoadSignatureAsync (assignment))
				.ContinueWith (_ => BeginInvokeOnMainThread (photoTableView.ReloadData));
		}

		/// <summary>
		/// Event when the "add photo" button is clicked
		/// </summary>
		partial void AddPhoto ()
		{
			photoViewModel.SelectedPhoto = new Photo { AssignmentId = assignmentViewModel.SelectedAssignment.Id, Date = DateTime.Now };
			photoSheet.ShowFrom (addPhoto.Frame, addPhoto.Superview, true);
		}

		/// <summary>
		/// Table source for photos
		/// </summary>
		class PhotoTableSource : UITableViewSource
		{
			const string Identifier = "PhotoCell";
			readonly PhotoViewModel photoViewModel;
			readonly ConfirmationController controller;

			public PhotoTableSource (ConfirmationController controller)
			{
				this.controller = controller;
				photoViewModel = ServiceContainer.Resolve<PhotoViewModel>();
			}

			public override UIView GetViewForHeader (UITableView tableView, nint section)
			{
				return new UIView { BackgroundColor = UIColor.Clear };
			}

			public override nint NumberOfSections (UITableView tableView)
			{
				return photoViewModel.Photos == null ? 0 : photoViewModel.Photos.Count;
			}

			public override nint RowsInSection (UITableView tableview, nint section)
			{
				return 1;
			}

			public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
			{
				var cell = tableView.DequeueReusableCell (Identifier) as PhotoCell;
				cell.SetPhoto (photoViewModel.Photos [indexPath.Section]);
				return cell;
			}

			public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
			{
				photoViewModel.SelectedPhoto = photoViewModel.Photos[indexPath.Section];

				var addPhotoController = controller.Storyboard.InstantiateViewController<AddPhotoController>();
				addPhotoController.Dismissed += (sender, e) => controller.ReloadConfirmation ();
				controller.PresentViewController(addPhotoController, true, null);
			}
		}

		/// <summary>
		/// Table source for signature
		/// </summary>
		class SignatureTableSource : UITableViewSource
		{
			const string SignatureIdentifier = "SignatureCell";
			const string CompleteIdentifier = "CompleteCell";
			readonly ConfirmationController controller;
			readonly AssignmentViewModel assignmentViewModel;

			public SignatureTableSource (ConfirmationController controller)
			{
				this.controller = controller;
				assignmentViewModel = ServiceContainer.Resolve<AssignmentViewModel>();
			}

			public override UIView GetViewForHeader (UITableView tableView, nint section)
			{
				return new UIView { BackgroundColor = UIColor.Clear };
			}

			public override nint NumberOfSections (UITableView tableView)
			{
				return 2;
			}

			public override nint RowsInSection (UITableView tableview, nint section)
			{
				return 1;
			}

			public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
			{
				UITableViewCell cell;
				if (indexPath.Section == 0) {
					cell = tableView.DequeueReusableCell (SignatureIdentifier);
					var signatureCell = cell as SignatureCell;
					signatureCell.SetSignature (controller, assignmentViewModel.SelectedAssignment, assignmentViewModel.Signature);
				} else {
					cell = tableView.DequeueReusableCell (CompleteIdentifier);
					var completeCell = cell as CompleteCell;
					completeCell.SetAssignment (controller, assignmentViewModel.SelectedAssignment, tableView);
				}
				return cell;
			}
		}
	}
}
