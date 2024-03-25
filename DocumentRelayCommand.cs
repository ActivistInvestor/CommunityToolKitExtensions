/// DocRelayCommand.cs 
/// ActivistInvestor / Tony T.
/// This code is based on (and dependent on)
/// types from CommunityToolkit.Mvvm.Input

using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.AutoCAD.Runtime;
using CommunityToolkit.Mvvm.Input;

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
   ///    Replace:          With:
   ///    -------------------------------------------------
   ///    RelayCommand      DocumentRelayCommand
   ///    RelayCommand<T>   DocumentRelayCommand<T>
   ///    
   /// In most cases the only change needed is the call to the
   /// constructor of the type.
   ///    
   /// That's all there is to it. After migration, your command
   /// implementation will run in the document execution context,
   /// and your command will only be executable when there is an
   /// active document.
   /// 
   /// Note that when your command executes, any currently-active 
   /// command(s) will be cancelled.
   /// 
   /// Roadmap:
   /// 
   /// 1. Extend IDocumentRelayCommand/<T>:
   /// 
   ///    CanExecute():
   ///    
   ///      1. Optionally disable command when 
   ///         the drawing editor is not quiescent.
   ///      
   ///      2. Disable the command while it executes
   ///         to disallow reentry (only needed if the
   ///         first item above is not implemented).
   ///         
   /// </summary>

   public class DocumentRelayCommand : IDocumentRelayCommand
   {
      private readonly Action execute;
      private readonly Func<bool>? canExecute;
      public event EventHandler? CanExecuteChanged;

      public DocumentRelayCommand(Action execute, Func<bool>? canExecute = null)
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
         return CommandContext.CanInvoke && this.canExecute?.Invoke() != false;
      }

      /// TT: Modified to execute in document execution context
      public void Execute(object? parameter)
      {
         CommandContext.Invoke(execute);
      }
   }

   public class DocumentRelayCommand<T> : IDocumentRelayCommand<T>
   {
      private readonly Action<T?> execute;
      private readonly Predicate<T?>? canExecute;
      public event EventHandler? CanExecuteChanged;

      public DocumentRelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
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
         return CommandContext.CanInvoke 
            && this.canExecute?.Invoke(parameter) != false;
      }

      public bool CanExecute(object? parameter)
      {
         if(!CommandContext.CanInvoke)
            return false;
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
      public static bool CanInvoke =>
         Application.DocumentManager.MdiActiveDocument != null;

      public static async void Invoke<T>(Action<T?> action, T? parameter)
      {
         ArgumentNullException.ThrowIfNull(action);
         if(!CanInvoke)
            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
         var docs = Application.DocumentManager;
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
         if(!CanInvoke)
            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
         var docs = Application.DocumentManager;
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

   /// <summary>
   /// Placeholders for future AutoCAD-specific extensions
   /// </summary>
   
   public interface IDocumentRelayCommand : IRelayCommand
   {
   }

   public interface IDocumentRelayCommand<in T> : IDocumentRelayCommand
   {
   }

}