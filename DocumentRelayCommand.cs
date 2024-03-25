﻿/// DocRelayCommand.cs 
/// ActivistInvestor / Tony T.
/// This code is based on (and dependent on)
/// types from CommunityToolkit.Mvvm.Input

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CommunityToolkit.Mvvm.Input;

namespace Autodesk.AutoCAD.ApplicationServices
{

   /// <summary>
   /// AutoCAD-specific implementations of IRelayCommand, and
   /// IRelayCommand<T> from CommunityToolkit.Mvvm that execute 
   /// command code in the document execution context. Both of
   /// these implementations also implement ICommand from .NET,
   /// allowing them to also replace ICommand-based types.
   /// 
   /// If you are currently using RelayCommand or RelayCommand<T>
   /// from CommunityToolkit.Mvvm you can easily migrate your code 
   /// to these types by making the following changes:
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
   /// command(s) will be cancelled. You can prevent your command
   /// from executing when a command is in progress by checking the
   /// CommandContext.IsQuiescent property and factor it into the
   /// the result of CanExecute() or the delegate passed to the
   /// constructor.
   /// 
   /// Use from existing implementations of ICommand:
   /// 
   /// If you currently have custom types that implement ICommand,
   /// you can just use the CommandContext.Invoke() methods to
   /// execute your commands from your existing ICommand's Execute()
   /// method.
   /// 
   /// You can also derive types from DocumentRelayCommand, and
   /// override Execute() and CanExecute() to specialize them with
   /// your custom functionality. Within your override of Execute()
   /// you can supermessage base.Execute() to run the command in the 
   /// document execution context, or call CommandContext.Invoke()
   /// directly.
   /// 
   /// Roadmap:
   /// 
   /// 1. Extend IDocumentCommand/<T>:
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
   /// Best practices for implementing command functionality 
   /// in MVVM scenarios:
   /// 
   /// It is wise to avoid placing command implementation code
   /// directly in an anonymous delegate passed to the constructor
   /// of an ICommand, because that prevents use of the code from
   /// various other non-UI contexts, such as making it available
   /// as a CommandMethod the user can invoke on the command line.
   /// 
   /// To make an implementation usable from various other contexts,
   /// it can be housed in a separate (possibly static), class that 
   /// allows it to be accessed from an ICommand, as well as from a 
   /// CommandMethod.
   /// 
   /// Note that in the code below, the AutoCAD-dependent code has
   /// been decoupled from the IDocumentRelayCommand implementations
   /// to support that same type of separation, and is what allows 
   /// the AutoCAD-specific code (in the CommandContext class) to be 
   /// accessable and usable from various other contexts. This also
   /// serves to prevent potential design-time failures from occuring 
   /// due to AutoCAD types appearing in code that may be jit'ed at 
   /// design-time.
   /// 
   /// </summary>

   public class DocumentRelayCommand : IDocumentCommand
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
      public virtual void Execute(object? parameter)
      {
         CommandContext.Invoke(execute);
      }
   }

   public class DocumentRelayCommand<T> : IDocumentCommand<T>
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
      /// TT: Modified to virtual
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public virtual bool CanExecute(T? parameter)
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

      /// TT: Modified to virtual
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public virtual void Execute(T? parameter)
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
         Execute(result);
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

   /// <summary>
   /// Helper class that also serves to encapsulate
   /// the use of AutoCAD types  and API calls.
   /// </summary>

   static class CommandContext
   {
      /// <summary>
      /// Called from the CanInvoke() implementations above
      /// </summary>

      public static bool CanInvoke =>
         Application.DocumentManager.MdiActiveDocument != null;

      /// <summary>
      /// Can be used to make an ICommand available only if
      /// there is no active command in the editor.
      /// </summary>

      public static bool IsQuiescent =>
         CanInvoke && Application.DocumentManager.MdiActiveDocument.Editor.IsQuiescent;

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
   
   public interface IDocumentCommand : IRelayCommand
   {
   }

   public interface IDocumentCommand<in T> : IRelayCommand<T>
   {
   }


   /// <summary>
   /// An ICommand Implementation that executes a
   /// single defined AutoCAD command whose name is
   /// passed into the constructor.
   /// </summary>

   public class DefinedDocumentCommand : ICommand
   {
      string commandName;

      public DefinedDocumentCommand(string commandName)
      {
         ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
         this.commandName = commandName;
      }

      public event EventHandler? CanExecuteChanged;

      public virtual bool CanExecute(object? parameter)
      {
         return CommandContext.IsQuiescent;
      }

      static Editor Editor
      {
         get 
         {
            if(docs.MdiActiveDocument == null)
               throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
            return docs.MdiActiveDocument.Editor;
         } 
      }

      static DocumentCollection docs = Application.DocumentManager;

      public virtual void Execute(object? parameter)
      {
         if(!string.IsNullOrWhiteSpace(commandName))
            CommandContext.Invoke(() => Editor.CommandAsync(commandName));
      }
   }

}