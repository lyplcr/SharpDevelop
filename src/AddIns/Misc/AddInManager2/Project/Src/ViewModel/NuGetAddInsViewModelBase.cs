﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows.Input;
using ICSharpCode.AddInManager2.Model;
using NuGet;

namespace ICSharpCode.AddInManager2.ViewModel
{
	public class NuGetAddInsViewModelBase : AddInsViewModelBase
	{
		private AddInManagerTask<ReadPackagesResult> _task;
		private IEnumerable<IPackage> _allPackages;
		
		public NuGetAddInsViewModelBase()
			: base()
		{
		}
		
		/// <summary>
		/// Returns all the packages.
		/// </summary>
		protected virtual IQueryable<IPackage> GetAllPackages()
		{
			return null;
		}
		
		public override void ReadPackages()
		{
			base.ReadPackages();
			_allPackages = null;
			UpdateRepositoryBeforeReadPackagesTaskStarts();
			StartReadPackagesTask();
		}
		
		private void StartReadPackagesTask()
		{
			IsReadingPackages = true;
			HasError = false;
			ClearPackages();
			CancelReadPackagesTask();
			CreateReadPackagesTask();
			_task.Start();
		}
		
		protected virtual void UpdateRepositoryBeforeReadPackagesTaskStarts()
		{
		}
		
		private void CancelReadPackagesTask()
		{
			if (_task != null)
			{
				_task.Cancel();
			}
		}
		
		private void CreateReadPackagesTask()
		{
			_task = AddInManagerTask.Create(
				() => GetPackagesForSelectedPageResult(),
				(result) => OnPackagesReadForSelectedPage(result));
		}
		
		private ReadPackagesResult GetPackagesForSelectedPageResult()
		{
			IEnumerable<IPackage> packages = GetPackagesForSelectedPage();
			return new ReadPackagesResult(packages, TotalItems);
		}
		
		private void OnPackagesReadForSelectedPage(AddInManagerTask<ReadPackagesResult> task)
		{
			IsReadingPackages = false;
			if (task.IsFaulted)
			{
				SaveError(task.Exception);
			}
			else if (task.IsCancelled)
			{
				// Ignore
			}
			else
			{
				UpdatePackagesForSelectedPage(task.Result);
			}
			base.OnPropertyChanged(null);
		}

		private void UpdatePackagesForSelectedPage(ReadPackagesResult result)
		{
			PagesCollection.TotalItems = result.TotalPackages;
			PagesCollection.TotalItemsOnSelectedPage = result.TotalPackagesOnPage;
			UpdatePackageViewModels(result.Packages);
		}
		
		private void PagesChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			StartReadPackagesTask();
			base.OnPropertyChanged(null);
		}
		
		private IEnumerable<IPackage> GetPackagesForSelectedPage()
		{
			IEnumerable<IPackage> filteredPackages = GetFilteredPackagesBeforePagingResults();
			return GetPackagesForSelectedPage(filteredPackages);
		}
		
		private IEnumerable<IPackage> GetFilteredPackagesBeforePagingResults()
		{
			if (_allPackages == null)
			{
				IQueryable<IPackage> packages = GetAllPackages();
				packages = OrderPackages(packages);
				packages = FilterPackagesBySearchCriteria(packages);
				TotalItems = packages.Count();
				_allPackages = GetFilteredPackagesBeforePagingResults(packages);
			}
			return _allPackages;
		}
		
		protected virtual IQueryable<IPackage> OrderPackages(IQueryable<IPackage> packages)
		{
			return packages
				.OrderBy(package => package.Id);
		}
		
		private IQueryable<IPackage> FilterPackagesBySearchCriteria(IQueryable<IPackage> packages)
		{
			string searchCriteria = GetSearchCriteria();
			return FilterPackagesBySearchCriteria(packages, searchCriteria);
		}
		
		private string GetSearchCriteria()
		{
			if (String.IsNullOrWhiteSpace(SearchTerms))
			{
				return null;
			}
			return SearchTerms;
		}

		protected IQueryable<IPackage> FilterPackagesBySearchCriteria(IQueryable<IPackage> packages, string searchCriteria)
		{
			return packages.Find(searchCriteria);
		}
		
		private IEnumerable<IPackage> GetPackagesForSelectedPage(IEnumerable<IPackage> allPackages)
		{
			int packagesToSkip = PagesCollection.ItemsBeforeFirstPage;
			return allPackages
				.Skip(packagesToSkip)
				.Take(PagesCollection.PageSize);
		}
		
		/// <summary>
		/// Allows filtering of the packages before paging the results. Call base class method
		/// to run default filtering.
		/// </summary>
		protected virtual IEnumerable<IPackage> GetFilteredPackagesBeforePagingResults(IQueryable<IPackage> allPackages)
		{
			return GetBufferedPackages(allPackages)
				.Where(package => package.IsReleaseVersion())
				.DistinctLast(PackageEqualityComparer.Id);
		}
		
		private IEnumerable<IPackage> GetBufferedPackages(IQueryable<IPackage> allPackages)
		{
			return allPackages.AsBufferedEnumerable(30);
		}
		
		protected virtual void UpdatePackageViewModels(IEnumerable<IPackage> packages)
		{
			IEnumerable<AddInPackageViewModelBase> currentViewModels = ConvertToAddInViewModels(packages);
			UpdatePackageViewModels(currentViewModels);
		}
		
		protected IEnumerable<AddInPackageViewModelBase> ConvertToAddInViewModels(IEnumerable<IPackage> packages)
		{
			foreach (IPackage package in packages)
			{
				yield return CreateAddInViewModel(package);
			}
		}
		
		protected virtual AddInPackageViewModelBase CreateAddInViewModel(IPackage package)
		{
			return new NuGetPackageViewModel(package);
		}
		
		public override int SelectedPageNumber
		{
			get
			{
				return base.SelectedPageNumber;
			}
			set
			{
				if (base.SelectedPageNumber != value)
				{
					base.SelectedPageNumber = value;
					StartReadPackagesTask();
					base.OnPropertyChanged(null);
				}
			}
		}
	}
}
