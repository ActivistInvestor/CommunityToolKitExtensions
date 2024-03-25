/// DocRelayCommand.cs 
/// ActivistInvestor / Tony T.
/// This code is based on (and dependent on)
/// types from CommunityToolkit.Mvvm.Input

using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.ApplicationServices
{

   /// <summary>
   /// AutoCAD-specific implementations of IRelayCommand, and
   /// IRelayCommand<T> from CommunityToolkit.Mvvm that execute 
   /// command code in the document execution context.
   /// 
   /// If you are currently using RelayCommand or RelayCommand<T>
   /// from CommunityToolkit.Mvvm, you can easily migrate your
   /// code to these types by making the following changes:
   /// 
   ///    Change            To
   ///    -------------------------------------------------
   ///    RelayCommand      DocRelayCommand
   ///    RelayCommand<T>   DocRelayCommand<T>
   ///    
   /// That's all there is to it. After migration, your command
   /// implementation will run in the document execution context,
   /// and your command will only be executable when there is an
   /// active document.
   /// 
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
         CommandContext.Invoke(execute);
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
         CommandContext.Invoke(execute, parameter);
      }

      /// TT: Modified to execute in document execution context
      public void Execute(object? parameter)
      {
         if(!TryGetCommandArgument(parameter, out T? result))
         {
            ThrowArgumentExceptionForInvalidCommandArgument(parameter);
         }
         CommandContext.Invoke(execute, result);
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

   static class CommandContext
   {
      public static async void Invoke<T>(Action<T> action, T? parameter)
      {
         ArgumentNullException.ThrowIfNull(action);
         var docs = Application.DocumentManager;
         if(docs.MdiActiveDocument == null)
            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
         if(docs.IsApplicationContext)
         {
            await docs.ExecuteInCommandContextAsync((_) =>
            {
               action(parameter);
               return Task.CompletedTask;
            }, null);
         }
         else
         {
            action(parameter);
         }
      }

      public static async void Invoke(Action action)
      {
         ArgumentNullException.ThrowIfNull(action);
         var docs = Application.DocumentManager;
         if(docs.MdiActiveDocument == null)
            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
         if(docs.IsApplicationContext)
         {
            await docs.ExecuteInCommandContextAsync((_) =>
            {
               action();
               return Task.CompletedTask;
            }, null);
         }
         else
         {
            action();
         }
      }
   }


}