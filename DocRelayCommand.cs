/// DocRelayCommand.cs 
/// ActivistInvestor / Tony T.
/// This code is based on (and dependent on)
/// types from CommunityToolkit.Mvvm.Input

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.ApplicationServices
{

   /// <summary>
   /// AutoCAD-specific implementations of IRelayCommand, and
   /// IRelayCommand<T> from CommunityToolkit.Mvvm that execute 
   /// the command in the document execution context.
   /// </summary>

   public class DocRelayCommand : CommunityToolkit.Mvvm.Input.IRelayCommand
   {
      private readonly Action execute;
      private readonly Func<bool>? canExecute;
      public event EventHandler? CanExecuteChanged;

      public DocRelayCommand(Action execute, Func<bool>? canExecute = null)
      {
         ArgumentNullException.ThrowIfNull(execute);
         this.execute = execute;
         this.canExecute = canExecute;
      }

      public void NotifyCanExecuteChanged()
      {
         CanExecuteChanged?.Invoke(this, EventArgs.Empty);
      }

      /// TT: Modified to return false if there is no document
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool CanExecute(object? parameter)
      {
         bool hasdoc = Application.DocumentManager.MdiActiveDocument != null;
         return hasdoc && this.canExecute?.Invoke() != false;
      }

      /// TT: Modified to execute in document execution context
      public void Execute(object? parameter)
      {
         var doc = Application.DocumentManager;
         if(doc.MdiActiveDocument == null)
            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
         doc.ExecuteInCommandContextAsync((_) =>
         {
            execute();
            return Task.CompletedTask;
         }, null);
      }
   }

   public sealed class DocRelayCommand<T> : CommunityToolkit.Mvvm.Input.IRelayCommand<T>
   {
      private readonly Action<T?> execute;
      private readonly Predicate<T?>? canExecute;
      public event EventHandler? CanExecuteChanged;

      public DocRelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
      {
         ArgumentNullException.ThrowIfNull(execute);
         this.execute = execute;
         this.canExecute = canExecute;
      }

      public void NotifyCanExecuteChanged()
      {
         CanExecuteChanged?.Invoke(this, EventArgs.Empty);
      }

      /// TT: Modified to return false if there is no document
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool CanExecute(T? parameter)
      {
         bool hasdoc = Application.DocumentManager.MdiActiveDocument != null;
         return hasdoc && this.canExecute?.Invoke(parameter) != false;
      }

      public bool CanExecute(object? parameter)
      {
         if(parameter is null && default(T) is not null)
         {
            return false;
         }
         if(!TryGetCommandArgument(parameter, out T? result))
         {
            ThrowArgumentExceptionForInvalidCommandArgument(parameter);
         }
         return CanExecute(result);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void Execute(T? parameter)
      {
         this.execute(parameter);
      }

      /// TT: Modified to execute in document execution context
      public void Execute(object? parameter)
      {
         if(!TryGetCommandArgument(parameter, out T? result))
         {
            ThrowArgumentExceptionForInvalidCommandArgument(parameter);
         }
         var doc = Application.DocumentManager;
         if(doc.MdiActiveDocument == null)
            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
         doc.ExecuteInCommandContextAsync((_) =>
         {
            Execute(result);
            return Task.CompletedTask;
         }, null);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      internal static bool TryGetCommandArgument(object? parameter, out T? result)
      {
         if(parameter is null && default(T) is null)
         {
            result = default;
            return true;
         }
         if(parameter is T argument)
         {
            result = argument;
            return true;
         }
         result = default;
         return false;
      }

      [DoesNotReturn]
      internal static void ThrowArgumentExceptionForInvalidCommandArgument(object? parameter)
      {
         [MethodImpl(MethodImplOptions.NoInlining)]
         static System.Exception GetException(object? parameter)
         {
            if(parameter is null)
            {
               return new ArgumentException($"Parameter \"{nameof(parameter)}\" (object) must not be null, as the command type requires an argument of type {typeof(T)}.", nameof(parameter));
            }
            return new ArgumentException($"Parameter \"{nameof(parameter)}\" (object) cannot be of type {parameter.GetType()}, as the command type requires an argument of type {typeof(T)}.", nameof(parameter));
         }
         throw GetException(parameter);
      }
   }


}