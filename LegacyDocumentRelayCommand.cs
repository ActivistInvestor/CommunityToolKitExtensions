/// LegacyDocumentRelayCommand.cs 
/// ActivistInvestor / Tony T.
/// This code is based (and dependent on)
/// types from CommunityToolkit.Mvvm.Input
/// 
/// This version of DocumentRelayCommand is
/// compatible with earlier versions of the 
/// .NET framework.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Runtime;

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
   /// active document and optionally, if the drawing editor is 
   /// in a quiescent state.
   /// 
   /// Note that when your command executes, any currently-active 
   /// command(s) will be cancelled. Since it's possible that the
   /// delegate that handles the command could interact with the
   /// user (e.g., call GetXxxxx() methods of the Editor), that
   /// opens the possiblity for the user to trigger reentry into 
   /// the command handler. Cancelling the current command serves 
   /// to eliminate that possiblity.
   /// 
   /// You can prevent your command from executing when a command 
   /// is in progress by setting the QuiescentOnly property to true.
   /// 
   /// Usage from existing implementations of ICommand:
   /// 
   /// If you currently have custom types that implement ICommand,
   /// you can just use the CommandContext.Invoke() method to run
   /// your commands from your ICommand's Execute() method.
   /// 
   /// Note that the code below keeps AutoCAD types and API calls 
   /// out of the ICommand-based types, and encapsulates that in 
   /// the CommandContext class, maintaining a clear separation 
   /// based on API depenence. 
   /// 
   /// Doing that serves to support unit testing and also prevent
   /// potential design-time failures from occuring due to AutoCAD 
   /// types appearing in code that may be jit'ed at design-time.
   /// </summary>

   public class DocumentRelayCommand : IDocumentCommand
   {
      private readonly Action execute;
      private readonly Func<bool> canExecute;
      private bool executing = false;

      public bool QuiescentOnly { get; set; } = false;

      public DocumentRelayCommand(Func<bool> canExecute, Action execute)
      {
         if(execute == null)
            throw new ArgumentNullException(nameof(execute));
         this.execute = execute;
         this.canExecute = canExecute;
      }

      public bool Executing
      {
         get { return executing; }
         protected set
         {
            if(executing ^ value)
            {
               executing = value;
               NotifyCanExecuteChanged();
            }
         }
      }

      public event EventHandler CanExecuteChanged;

      public void NotifyCanExecuteChanged()
      {
         CanExecuteChanged?.Invoke(this, EventArgs.Empty);
      }

      /// TT: Modified to return true if:
      /// 
      ///     1. A canExecute delegate was supplied to the constructor
      ///        and it returned true. No other conditions are evaluated.
      ///        
      ///     or:
      ///     
      ///     1. A canExecute delegate was not supplied to the constructor, and
      ///     2. There is an active document, and
      ///     3. The document is quiescent or QuiescentOnly is false, and
      ///     4. The command is not currently executing.
      ///

      public bool CanExecute(object parameter)
      {
         if(canExecute != null)
            return canExecute();
         return !Executing && CommandContext.CanInvoke(QuiescentOnly);
      }

      /// <summary>
      /// Had to make this async and await Invoke():
      /// 
      /// This is done to ensure that any exception
      /// thrown by the delegate will not be discarded,
      /// and also because without intimate knowledge,
      /// one must assume callers expect the command 
      /// to be completed when this returns.
      /// </summary>

      public async void Execute(object parameter)
      {
         Executing = true;
         try
         {
            await CommandContext.Invoke(execute);
         }
         finally 
         { 
            Executing = false;
         }
      }
   }

   public class DocumentRelayCommand<T> : IDocumentCommand<T>
   {
      private readonly Action<T> execute;
      private readonly Func<T, bool> canExecute;
      public bool QuiescentOnly { get; set; } = false;
      public event EventHandler CanExecuteChanged;
      bool executing = false;

      public DocumentRelayCommand(Func<T, bool> canExecute, Action<T> execute)
      {
         if(execute == null)
            throw new ArgumentNullException(nameof(execute));
         this.execute = execute;
         this.canExecute = canExecute;
      }

      public bool Executing
      {
         get { return executing; }
         protected set
         {
            if(executing ^ value)
            {
               executing = value;
               NotifyCanExecuteChanged();
            }
         }
      } 

      public void NotifyCanExecuteChanged()
      {
         CanExecuteChanged?.Invoke(this, EventArgs.Empty);
      }

      /// TT: Modified to return true if:
      /// 
      ///     1. A canExecute delegate was supplied to the constructor
      ///        and it returned true. No other conditions are evaluated.
      ///        
      ///   Or:
      ///     
      ///     1. A canExecute delegate was not supplied to the constructor, and
      ///     2. There is an active document, and
      ///     3. The document is quiescent or QuiescentOnly is false, and
      ///     4. The command is not currently executing.
      ///

      public bool CanExecute(T parameter)
      {
         if(canExecute != null)
            return canExecute(parameter);
         return !Executing && CommandContext.CanInvoke(QuiescentOnly);
      }

      /// <summary>
      /// TT: Add this because the original code was 
      /// evaluating it in every call to CanExecute().
      /// </summary>
      static readonly bool argIsNotNullable = default(T) != null;

      /// TT: I don't like this at all.
      public bool CanExecute(object parameter)
      {
         if(argIsNotNullable && parameter is null)
         {
            return false;
         }
         if(!TryGetCommandArgument(parameter, out T result))
         {
            ThrowArgumentExceptionForInvalidCommandArgument(parameter);
         }
         return CanExecute(result);
      }

      public async void Execute(T parameter)
      {
         Executing = true;
         try
         {
            await CommandContext.Invoke(execute, parameter);
         }
         finally
         {
            Executing = false;
         }
      }

      public void Execute(object parameter)
      {
         if(!TryGetCommandArgument(parameter, out T result))
         {
            ThrowArgumentExceptionForInvalidCommandArgument(parameter);
         }
         Execute(result);
      }

      internal static bool TryGetCommandArgument(object parameter, out T result)
      {
         if(parameter is null && default(T) == null)
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

      /// [DoesNotReturn]
      internal static void ThrowArgumentExceptionForInvalidCommandArgument(object parameter)
      {
         System.Exception GetException(object argument)
         {
            if(argument is null)
            {
               return new ArgumentException($"Parameter \"{nameof(argument)}\" (object) must not be null, as the command type requires an argument of type {typeof(T)}.", nameof(argument));
            }
            return new ArgumentException($"Parameter \"{nameof(argument)}\" (object) cannot be of type {argument.GetType()}, as the command type requires an argument of type {typeof(T)}.", nameof(argument));
         }
         throw GetException(parameter);
      }
   }


   /// <summary>
   /// This class encapsulates and isolates all AutoCAD API-
   /// dependent functionality. Generally, types that use the
   /// methods of this class should not contain AutoCAD types 
   /// or method calls, espcially if they contain methods that
   /// can be jit'd at design-time.
   /// </summary>

   internal static class CommandContext
   {
      static readonly DocumentCollection docs = Application.DocumentManager;

      /// <summary>
      /// Gets a value indicating if a command can execute 
      /// based on two conditions:
      /// 
      ///   1. If there is an open document.
      ///   2. If there is an open document 
      ///      that is in a quiescent state.
      ///      
      /// The arguments specify which of the conditions are
      /// applicable and tested.
      /// </summary>
      /// <param name="quiescentOnly">A value indicating if the
      /// operation cannot be performed if the document is not 
      /// quiescent</param>
      /// <param name="documentRequired">A value indicating if the
      /// operation can be performed if there is no active document</param>
      /// <returns>A value indicating if the operation can be performed</returns>

      public static bool CanInvoke(bool quiescentOnly = false, bool documentRequired = true)
      {
         Document doc = docs.MdiActiveDocument;
         return (!documentRequired || doc != null)
            && (!quiescentOnly || doc.Editor.IsQuiescent);
      }

      /// <summary>
      /// Returns a value indicating if the active 
      /// document is quiescent.
      /// Returns false if there is no document.
      /// </summary>

      public static bool IsQuiescent =>
         docs.MdiActiveDocument?.Editor.IsQuiescent == true;

      /// <summary>
      /// Return a value indicating if there is an active document
      /// </summary>
      public static bool HasDocument => docs.MdiActiveDocument != null;

      /// <summary>
      /// Return a value indicating if there is an active document
      /// and it is quiescent.
      /// </summary>
      
      public static bool HasQuiescentDocument
      {
         get
         {
            var doc = docs.MdiActiveDocument;
            return doc != null && doc.Editor.IsQuiescent;
         }
      }

      /// <summary>
      /// Invokes the given action in the document execution context
      /// </summary>
      /// <typeparam name="T">The type of the argument passed to the action</typeparam>
      /// <param name="action">The action to execute</param>
      /// <param name="parameter">The value to pass as the parameter to the action</param>
      /// <returns>A Task</returns>
      /// <exception cref="Autodesk.AutoCAD.Runtime.Exception"></exception>
      public static async Task Invoke<T>(Action<T> action, T parameter = default)
      {
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         if(docs.MdiActiveDocument == null)
            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
         if(docs.IsApplicationContext)
         {
            await docs.ExecuteInCommandContextAsync((unused) =>
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

      /// <summary>
      /// Invokes the given action in the document execution context
      /// </summary>
      /// <param name="action">The action to execute</param>
      /// <returns>A Task</returns>
      /// <exception cref="Autodesk.AutoCAD.Runtime.Exception"></exception>

      public static async Task Invoke(Action action)
      {
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         if(docs.MdiActiveDocument == null)
            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
         if(docs.IsApplicationContext)
         {
            await docs.ExecuteInCommandContextAsync((unused) =>
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

   public interface IDocumentCommand 
   {
      /// <summary>
      /// Gets/sets a value indicating if the command can
      /// execute only when the editor is quiescent.
      /// </summary>
      bool QuiescentOnly { get; set; }
      void NotifyCanExecuteChanged();

   }

   public interface IDocumentCommand<in T> : IDocumentCommand
   {
      bool CanExecute(T parameter);
      void Execute(T parameter);
   }


   /// <summary>
   /// Experimental:
   /// 
   /// An ICommand Implementation that executes a defined 
   /// AutoCAD command whose CommandMethod method is passed
   /// into the constructor. 
   /// 
   /// The method passed into the constructor must be static, 
   /// and must have the CommandMethod attribute applied to it.
   /// 
   /// If the CommandMethod attribute applied to the method
   /// has CommandFlags.Session in its command flags, then
   /// the method will be invoked in the application context.
   /// Otherwise, it is invoked in the document context.
   /// 
   /// This type cannot support non-static command methods,
   /// because Autodesk did not provide a way to access the
   /// instance of the containing type that it creates for 
   /// each document, when the containing type contains one
   /// or more non-static command methods.
   /// 
   /// Unless there is a need for per-document state or per-
   /// document events, non-static command methods should be 
   /// avoided.
   /// </summary>

   public class RegisteredCommand : IDocumentCommand
   {
      Action commandMethod;
      bool appContext = false;
      bool executing = false;

      public RegisteredCommand(Action commandMethod)
      {
         if(commandMethod == null)
            throw new ArgumentNullException(nameof(commandMethod));
         MethodInfo m = commandMethod.GetMethodInfo();
         if(!m.IsStatic)
            throw new ArgumentException("Requires a static CommandMethod");
         CommandMethodAttribute att = m.GetCustomAttribute<CommandMethodAttribute>();
         if(att == null)
            throw new ArgumentException("Requires a static CommandMethod");
         appContext = att.Flags.HasFlag(CommandFlags.Session);
         this.commandMethod = commandMethod;
      }

      protected bool Executing
      {
         get { return executing; }
         set
         {
            if(executing ^ value)
            {
               executing = value;
               NotifyCanExecuteChanged();
            }
         }
      }

      public bool QuiescentOnly { get; set;}

      public event EventHandler CanExecuteChanged;

      public virtual bool CanExecute(object parameter)
      {
         return !Executing && CommandContext.CanInvoke(QuiescentOnly);
      }

      public virtual async void Execute(object parameter)
      {
         Executing = false;
         try
         {
            if(appContext)
               commandMethod();
            else
               await CommandContext.Invoke(() => commandMethod());
         }
         finally
         { 
            Executing = true; 
         }
      }

      public void NotifyCanExecuteChanged()
      {
         CanExecuteChanged?.Invoke(this, EventArgs.Empty);
      }
   }

}