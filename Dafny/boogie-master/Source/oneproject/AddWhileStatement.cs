//------------------------------------------------------------------------------
// <copyright file="Command1.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Dafny;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;

namespace oneproject
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class AddWhileStatement
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;
        
        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("93397e01-1c66-4aeb-b900-376690e18336");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="AddWhileStatement"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private AddWhileStatement(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static AddWhileStatement Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new AddWhileStatement(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            
            string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
            string title = "Extract Method and Add While Statement";
            var textManager = this.ServiceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
            IVsTextView textview;
            textManager.GetActiveView(1, null, out textview);
            var componentModel = this.ServiceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            var text = componentModel.GetService<IVsEditorAdaptersFactoryService>().GetWpfTextView(textview);
            int selection = text.Caret.Position.BufferPosition.GetContainingLine().LineNumber+1;
            int end = text.Selection.End.Position;
            selection = text.Selection.Start.Position.GetContainingLine().LineNumber+1;
 

            string filename = GetFileName(text);
            
            Method m = FindMethod(filename, selection);
            BuildBlock(text,m);
            
             }

        private void BuildBlock(IWpfTextView text,Method method)
        {
              string newMethodName = GetNewName(text);
            UpdateStmt firstStmt;
            int i = 0;


            AssertStmt pre=null, post=null;
            Expression invariant=null;
            Expression guard = ExtractGuard(method.Ens.Last().E);
            if (method.Ens.Last().E is BinaryExpr)
            {
                var bexp = method.Ens.Last().E as BinaryExpr;
                if(bexp.Op == BinaryExpr.Opcode.And)
                {
                    guard = InvertExp(bexp.E1);
                    post = new AssertStmt(null,null,bexp, null,null);
                    pre = new AssertStmt(null, null, bexp.E0, null, null);
                    invariant = bexp.E0;
                }
                    
            }

                //new UnaryOpExpr(null, UnaryOpExpr.Opcode.Not,ExtractGuard(method.Ens.Last().E));
            List<Expression> decress = new List<Expression>();
                var decExp=new BinaryExpr(null, BinaryExpr.Opcode.Sub, ((BinaryExpr)guard).E0, ((BinaryExpr)guard).E1);
                decress.Add(decExp);

            List<MaybeFreeExpression> invs = new List<MaybeFreeExpression>();
            invs.Add(new MaybeFreeExpression(invariant));
            var newStmts = new List<Statement>();
            var whileBodystmts = new List<Statement>();
            if (BuildFirstStatement(method.Ins, method.Outs, out firstStmt)) 
                newStmts.Add(firstStmt);

                List<Expression> args = new List<Expression>();
                foreach(var v in GetAllVars(method.Ins, method.Outs))
                {
                    args.Add(new NameSegment(null, v.Name, null));
                }
                AssignmentRhs arhs = new ExprRhs(new ApplySuffix(null,new NameSegment(null,newMethodName,null)
                    ,args));
                var lstRhs = new List<AssignmentRhs>();
                lstRhs.Add(arhs);
                whileBodystmts.Add(new UpdateStmt(null, null, firstStmt.Lhss, lstRhs));
                    
            BlockStmt whileBody = new BlockStmt(null, null, whileBodystmts);
            WhileStmt wstmt = new WhileStmt(null, null, guard,invs , new Specification<Expression>(decress, null) , new Specification<FrameExpression>(null,null), whileBody);
        

            newStmts.Add(pre);
            newStmts.Add(wstmt);
            newStmts.Add(post);
            BlockStmt funcBody = new BlockStmt(null, null, newStmts);

            List<MaybeFreeExpression> newMethodreq = new List<MaybeFreeExpression>();
            List<MaybeFreeExpression> newMethodens = new List<MaybeFreeExpression>();
            newMethodreq.Add(new MaybeFreeExpression(new BinaryExpr(null,BinaryExpr.Opcode.And, 
                ReplaceOutsWithIns(invariant, method.Ins, method.Outs),
                ReplaceOutsWithIns(guard, method.Ins, method.Outs))));

            newMethodens.Add(new MaybeFreeExpression(new BinaryExpr(null, BinaryExpr.Opcode.And, invariant, 
                new BinaryExpr(null,BinaryExpr.Opcode.Le,decExp, ReplaceOutsWithIns(decExp, method.Ins, method.Outs)))));

            Method m = new Method(null, newMethodName, false, false, new List<TypeParameter>(),
               method.Ins, method.Outs , newMethodreq,
                new Specification<FrameExpression>(null, null), newMethodens, new Specification<Expression>(null, null), null, null, null);

            int selection = text.Selection.End.Position;
            selection = GetPositionOFLastToken(method,text);
            string s=Printer.StatementToString(funcBody)+"\n\n";
            string f = "\n\n"+Printer.MethodSignatureToString(m)+"\n";
            WriteToTextViewer(f, text, selection);
            WriteToTextViewer(s,text, selection);
        }

        private bool BuildFirstStatement(List<Formal> ins, List<Formal> outs, out UpdateStmt firstStmt)
        {
            List<Expression> lhs = new List<Expression>();
            List<AssignmentRhs> rhs = new List<AssignmentRhs>();
            int n;
            
            for(int i = 0; i < outs.Count; i++)
            {
                for(int j = 0; j < ins.Count; j++)
                {
                    if (ins[j].Name.Contains(outs[i].Name) && int.TryParse(ins[j].Name.Substring(outs[i].Name.Length), out n))
                    {
               //         List<Microsoft.Dafny.Type> l = new List<Microsoft.Dafny.Type>(), r = new List<Microsoft.Dafny.Type>();
                        lhs.Add(new NameSegment(null,outs[i].Name,null));
                        rhs.Add(new ExprRhs( new NameSegment(null,ins[j].Name,null)));
                        j = ins.Count;
                    }
                }
            }

            firstStmt =  new UpdateStmt(null, null, lhs, rhs);
            return lhs.Count != 0;
        }

        private Expression ReplaceOutsWithIns(Expression expr, List<Formal> ins, List<Formal> outs)
        {

            if (expr is NameSegment)
            {
                var exp = expr as NameSegment;
                foreach (var v in outs)
                {
                    if (v.Name.CompareTo(exp.Name) == 0)
                    {
                        return new NameSegment(null, ReplaceOutVarwithIn(v, ins).Name, null);
                    }
                }
                return exp;
            }else
            if (expr is UnaryOpExpr)
            {
                var exp = expr as UnaryOpExpr;
                return new UnaryOpExpr(null,exp.Op,ReplaceOutsWithIns(exp.E, ins, outs));
            }
            else
            if (expr is BinaryExpr)
            {
                var exp = expr as BinaryExpr;
                Expression e0 = exp.E0;
                Expression e1 = exp.E1;
               
                return new BinaryExpr(null, exp.Op, ReplaceOutsWithIns(e0,ins,outs), ReplaceOutsWithIns(e1, ins, outs));
            }else if (expr is ApplySuffix)
            {
                var exp = expr as ApplySuffix;
                var args = new List<Expression>();
                foreach(var v in exp.Args)
                {
                    args.Add(ReplaceOutsWithIns(v, ins, outs));
                }
                return new ApplySuffix(null,exp.Lhs, args);
            }else if(expr is SeqSelectExpr)
            {
                var exp = expr as SeqSelectExpr;
                return new SeqSelectExpr(null, exp.SelectOne, exp.Seq,ReplaceOutsWithIns(exp.E0, ins, outs),
                    ReplaceOutsWithIns(exp.E1, ins, outs));
            }
            else if (expr is ParensExpression)
            {
                var exp = expr as ParensExpression;
                return new ParensExpression(null, ReplaceOutsWithIns(exp.E, ins, outs));
            }
            else if (expr is ChainingExpression)
            {
                var exp = expr as ChainingExpression;
                List<Expression> rands = new List<Expression>();
                foreach(var v in exp.Operands)
                {
                    rands.Add(ReplaceOutsWithIns(v, ins, outs));
                }
                return new ChainingExpression(null, rands, exp.Operators, exp.OperatorLocs, exp.PrefixLimits);
            }
            else if (expr is SeqDisplayExpr)
            {
                var exp = expr as SeqDisplayExpr;
                List<Expression> elements = new List<Expression>();
                foreach (var v in exp.Elements)
                {
                    elements.Add(ReplaceOutsWithIns(v, ins, outs));
                }
                return new SeqDisplayExpr(null, elements);
            }
                return null;
        }
        private Formal ReplaceOutVarwithIn(Formal outV, List<Formal> ins)
        {
            int n;
            for (int j = 0; j < ins.Count; j++)
            {
                if (ins[j].Name.Contains(outV.Name) && int.TryParse(ins[j].Name.Substring(outV.Name.Length), out n))
                {
                    return ins[j];
                }
            }
            return outV;
        }
        private List<Formal> ReplaceOutsWithIns(List<Formal> ins, List<Formal> outs)
        {
            List<Formal> parameters = new List<Formal>();
            for (int i = 0; i < outs.Count; i++)
                parameters.Add(outs[i]);
            int n;


                for (int i = 0; i < outs.Count; i++)
                {
                    for (int j = 0; j < ins.Count; j++)
                    {
                    if (ins[j].Name.Contains(outs[i].Name) && int.TryParse(ins[j].Name.Substring(outs[i].Name.Length), out n))
                    {
                        parameters[i] = ins[j];
                        j = outs.Count;
                    }
                }
            }

            return parameters;

        }
        private List<Formal> GetAllVars(List<Formal> ins, List<Formal> outs)
        {
            List<Formal> parameters = new List<Formal>();
            for (int i = 0; i < ins.Count; i++)
                parameters.Add(ins[i]);
            int n;

            for (int j = 0; j < ins.Count; j++)
            {
                for (int i = 0; i < outs.Count; i++)
                {
                    if (ins[j].Name.Contains(outs[i].Name) && int.TryParse(ins[j].Name.Substring(outs[i].Name.Length), out n))
                    {
                        parameters[j]=outs[i];
                        i = outs.Count;
                    }
                }
            }

            return parameters;

        }

        private Expression InvertExp(Expression exp)
        {
            if (exp is BinaryExpr)
            {
                var expr = exp as BinaryExpr;
                if (expr.Op == BinaryExpr.Opcode.Eq)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Neq, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Neq)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Eq, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Gt)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Le, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Le)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Gt, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Ge)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Lt, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Lt)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Ge, expr.E0, expr.E1);
                //       var ans = ExtractGuard(expr.E0);
                //       if (ans == null)
            }
            return new UnaryOpExpr(null, UnaryOpExpr.Opcode.Not, exp);
        }

        private int GetPositionOFLastToken(Method method,IWpfTextView text)
        {

            if (method.Ens.Count > 0)
            {
                return text.TextSnapshot.GetLineFromLineNumber(method.Ens.Last().E.tok.line-1).EndIncludingLineBreak+1;
            }
            if(method.Req.Count>0)
            {
                return text.TextSnapshot.GetLineFromLineNumber(method.Req.Last().E.tok.line - 1).EndIncludingLineBreak + 1;
            }
            return text.TextSnapshot.GetLineFromLineNumber(method.tok.line - 1).EndIncludingLineBreak + 1;
        }

        private void AddInOutAssignment(List<DVariable> ins)
        {
             foreach(var v in ins)
            {
                v.name = v.name + '0';
            }
        }

        public HashSet<DVariable> GetVars(List<Statement> statements, out HashSet<DVariable> declVars, bool containNonModified)
        {
            DVariableComparer comparer = new DVariableComparer();
            HashSet<DVariable> vars = new HashSet<DVariable>(comparer);
            declVars = new HashSet<DVariable>(comparer);
            foreach (var stmt in statements)
            {
                vars.UnionWith(GetVars(stmt, declVars, containNonModified));
            }

            return vars;

        }
        private HashSet<DVariable> GetVars(Statement stmt, HashSet<DVariable> declaredVars, bool containNonModified)
        {
            DVariableComparer comparer = new DVariableComparer();
            HashSet<DVariable> usedVars = new HashSet<DVariable>(comparer);
            if (stmt is UpdateStmt)
            {
                var ustmt = (UpdateStmt)stmt;
                foreach (var ls in ustmt.Lhss)
                {
                    usedVars.UnionWith(GetVars(ls, declaredVars));
                }
                if (containNonModified)
                {
                    foreach (var rs in ustmt.Rhss)
                    {
                        var exp = rs as ExprRhs;
                        usedVars.UnionWith(GetVars(exp.Expr, declaredVars));
                    }
                }
            }
            else if (stmt is AssertStmt && containNonModified)
            {
                var asrt = stmt as AssertStmt;
                usedVars.UnionWith(GetVars(asrt.Expr, declaredVars));
                usedVars.UnionWith(GetVars(asrt.Proof, declaredVars, containNonModified));

            }
            else if (stmt is WhileStmt)
            {
                var wstmt = stmt as WhileStmt;
                usedVars.UnionWith(GetVars(wstmt.Body, declaredVars, containNonModified));
                foreach (var exp in wstmt.Decreases.Expressions)
                    usedVars.UnionWith(GetVars(exp, declaredVars));
                usedVars.UnionWith(GetVars(wstmt.Guard, declaredVars));
                foreach (var exp in wstmt.Invariants)
                    usedVars.UnionWith(GetVars(exp.E, declaredVars));
            }
            else if (stmt is BlockStmt)
            {
                var stmts = stmt as BlockStmt;
                foreach (var bodyStmt in stmts.Body)
                {
                    usedVars.UnionWith(GetVars(bodyStmt, declaredVars, containNonModified));
                }

            }
            else if (stmt is VarDeclStmt)
            {
                var decl = stmt as VarDeclStmt;
                usedVars.UnionWith(GetVars(decl.Update, declaredVars, containNonModified));
                if (decl.Locals != null)
                {
                    foreach (var v in decl.Locals)
                    {
                        DVariable dvar = new DVariable(v.DisplayName, v.Type);
                        declaredVars.Add(dvar);
                    }
                }

            }
            else if (stmt is IfStmt)
            {
                var ifstmt = stmt as IfStmt;
                usedVars.UnionWith(GetVars(ifstmt.Guard, declaredVars));
                usedVars.UnionWith(GetVars(ifstmt.Thn, declaredVars, containNonModified));
                usedVars.UnionWith(GetVars(ifstmt.Els, declaredVars, containNonModified));
            }
            else if (stmt is PrintStmt)
            {
                var pstmt = stmt as PrintStmt;
                foreach (var arg in pstmt.Args)
                    usedVars.UnionWith(GetVars(arg, declaredVars));
            }

            return usedVars;
        }

        private HashSet<DVariable> GetVars(Expression exp, HashSet<DVariable> declaredVars)
        {
            DVariableComparer comparer = new DVariableComparer();
            HashSet<DVariable> vars = new HashSet<DVariable>(comparer);
            if (exp is SeqSelectExpr)
            {
                var expr = exp as SeqSelectExpr;
                vars.UnionWith(GetVars(expr.Seq, declaredVars));
                vars.UnionWith(GetVars(expr.E0, declaredVars));
                vars.UnionWith(GetVars(expr.E1, declaredVars));
            }
            else if (exp is NameSegment)
            {
                var expr = exp as NameSegment;
                DVariable var = new DVariable(expr.Name, expr.Type);
                if (!declaredVars.Contains(var))
                {

                    vars.Add(var);
                }

            }
            else if (exp is ApplySuffix)
            {
                var expr = exp as ApplySuffix;
                foreach (var arg in expr.Args)
                    vars.UnionWith(GetVars(arg, declaredVars));
            }
            else if (exp is BinaryExpr)
            {
                var expr = exp as BinaryExpr;
                vars.UnionWith(GetVars(expr.E0, declaredVars));
                vars.UnionWith(GetVars(expr.E1, declaredVars));
            }
            else if (exp is UnaryOpExpr)
            {
                var expr = exp as UnaryOpExpr;
                vars.UnionWith(GetVars(expr.E, declaredVars));
            }
            else if (exp is ParensExpression)
            {
                var expr = exp as ParensExpression;
                vars.UnionWith(GetVars(expr.E, declaredVars));
            }
            else if (exp is ChainingExpression)
            {
                var expr = exp as ChainingExpression;
                vars.UnionWith(GetVars(expr.E, declaredVars));
            }
            else if (exp is SeqDisplayExpr)
            {
                var expr = exp as SeqDisplayExpr;
                foreach (var arg in expr.Elements)
                    vars.UnionWith(GetVars(arg, declaredVars));
            }
            else if (exp is ForallExpr)
            {
                var expr = exp as ForallExpr;
                var newDecVars = new HashSet<DVariable>(declaredVars, comparer);
                if (expr.BoundVars != null)
                {
                    foreach (var bvar in expr.BoundVars)
                    {
                        DVariable dvar = new DVariable(bvar.DisplayName, bvar.Type);
                        newDecVars.Add(dvar);
                    }
                }
                vars.UnionWith(GetVars(expr.Term, newDecVars));
            }
            return vars;
        }
        public void WriteToTextViewer(string codeToWrite, IWpfTextView viewer, int offset)
        {
            Microsoft.VisualStudio.Text.ITextEdit ie = viewer.Selection.TextView.TextBuffer.CreateEdit();
            ie.Insert(offset, codeToWrite);
            ie.Apply();
        }
        public void GetConditions(Method method, int offset, out AssertStmt preCond, out AssertStmt postCond)
        {
            List<Statement> body = method.Body.Body;
            List<Statement> lst = body.Where(m => m is AssertStmt).ToList();
            int max = int.MinValue;
            int min = int.MaxValue;
            preCond = null;
            postCond = null;
            foreach (var statement in lst)
            {
                if (statement.Tok.pos < offset)
                {
                    if (statement.Tok.pos - offset > max)
                    {
                        max = statement.Tok.pos - offset;
                        preCond = statement as AssertStmt;

                    }
                }
                else
                        if (offset - statement.Tok.pos < min)
                {
                    min = offset - statement.Tok.pos;
                    postCond = statement as AssertStmt;

                }
            }


        }

        private string GetFileName(IWpfTextView textView)
        {
            ITextDocument doc;
            textView.TextBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out doc);
            return doc.FilePath;
        }

 
        public BinaryExpr ExtractGuard(Expression exp)
        {

            if (exp is BinaryExpr)
            {
                var expr = exp as BinaryExpr;
                if (expr.Op == BinaryExpr.Opcode.Eq)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Neq, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Neq)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Eq, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Gt)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Le, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Le)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Gt, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Ge)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Lt, expr.E0, expr.E1);
                if (expr.Op == BinaryExpr.Opcode.Lt)
                    return new BinaryExpr(null, BinaryExpr.Opcode.Ge, expr.E0, expr.E1);
                //       var ans = ExtractGuard(expr.E0);
                //       if (ans == null)
                return ExtractGuard(expr.E1);
          //      return ans;
            }
            else if (exp is UnaryOpExpr)
            {
                var expr = exp as UnaryOpExpr;
            }
            else if (exp is ParensExpression)
            {
                var expr = exp as ParensExpression;
                return ExtractGuard(expr.E);
            }
            else if (exp is ChainingExpression)
            {
                var expr = exp as ChainingExpression;
                return ExtractGuard(expr.E);
            }
            else if (exp is SeqDisplayExpr)
            {
                var expr = exp as SeqDisplayExpr;

            }
            return null;

        }

        public List<Statement> GetSelectedStatements(Method method, int start, int end, out List<Statement> otherStatements)
        {
            List<Statement> statements = new List<Statement>();
            otherStatements = new List<Statement>();
            ExtendSelectedStatements(method.Body, start, end, statements, otherStatements);
            

            return statements;
        }
        private void ExtendSelectedStatements(Statement statement, int start, int end, List<Statement> statements, List<Statement> otherStatements)
        {

            if (statement.Tok.pos >= start && statement.EndTok.pos <= end)
                statements.Add(statement);
            else if (statement.Tok.pos > end)
                otherStatements.Add(statement);
            else if (statement.Tok.pos <= start && statement.Tok.pos < end && statement.EndTok.pos >= end)
            {
                if (statement is WhileStmt)
                {
                    var wstmt = statement as WhileStmt;
                    ExtendSelectedStatements(wstmt.Body, start, end, statements, otherStatements);
                }
                else if (statement is BlockStmt)
                {
                    var stmts = statement as BlockStmt;
                    foreach (var inStmt in stmts.Body)
                        ExtendSelectedStatements(inStmt, start, end, statements, otherStatements);

                }
                else if (statement is IfStmt)
                {
                    var ifstmt = statement as IfStmt;
                    ExtendSelectedStatements(ifstmt.Thn, start, end, statements, otherStatements);
                    ExtendSelectedStatements(ifstmt.Els, start, end, statements, otherStatements);
                }

            }
        }

        public List<AssertStmt> GetRequires(List<Statement> statements)
        {
            List<AssertStmt> requires = new List<AssertStmt>();

            foreach (var stmt in statements)
            {
                if (stmt is AssertStmt)
                    requires.Add(stmt as AssertStmt);
                else break;
            }
            foreach(var v in requires)
                statements.Remove(v);
            return requires;
        }

        public List<AssertStmt> GetEnsures(List<Statement> statements)
        {
            List<AssertStmt> ensures = new List<AssertStmt>();

            for (int i = statements.Count - 1; i > -1; i--)
            {
                var stmt = statements[i];
                if (stmt is AssertStmt)
                    ensures.Add(stmt as AssertStmt);
                else break;
            }
            foreach (var v in ensures)
                statements.Remove(v);
            return ensures;
        }

        public void GetVariables(List<Statement> statements, List<Statement> otherStatements, out List<DVariable> ins, out List<DVariable> outs, out List<DVariable> toDeclare)
        {
            DVariableComparer comparer = new DVariableComparer();
            HashSet<DVariable> varsDeclaredInSelectedScope;
            var varsUsedInSelectedScope = GetVars(statements, out varsDeclaredInSelectedScope, true);
            var varsModifiedInSelectedScope = GetModifiedVariables(statements);
            HashSet<DVariable> varsDeclaredAfterScope;
            var varsUsedAfterScope = GetVars(otherStatements, out varsDeclaredAfterScope, true);
            ins = varsUsedInSelectedScope.ToList();
            //          outs = varsModifiedInSelectedScope.Intersect(varsUsedAfterScope, comparer).ToList();
            outs = varsModifiedInSelectedScope; 
            toDeclare = varsDeclaredInSelectedScope.Intersect(varsUsedAfterScope, comparer).ToList();

        }

        public List<DVariable> GetModifiedVariables(List<Statement> stmts)
        {
            HashSet<DVariable> t;
            return GetVars(stmts, out t, false).ToList();
        }
        public string buildMethod(Method m)
        {
            string ans = Printer.MethodSignatureToString(m);
            ans += Printer.StatementToString(m.Body);
            return ans;
        }
        public string GetNewName(IWpfTextView text)
        {
            int i = 0;
            string basename = "MM";
           
            while (true) {
                bool flag = false;
                foreach (var v in getNames(GetFileName(text)))
                {
                    if (v.CompareTo(basename + i) == 0)
                    {
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                    return basename + i;
                i++;
            }

        }
        public List<string> getNames(string filename)
        {
            var program = getProgram(filename);
            List<TopLevelDecl> decls = new List<TopLevelDecl>();
            foreach (var module in program.Modules())
            {
                decls.AddRange(module.TopLevelDecls);
            }
            List<string> lst = new List<string>();
            var callables = ModuleDefinition.AllCallables(decls);
            foreach (var decl in callables)
            {
                if (decl is Method)
                    lst.Add(((Method)decl).Name);
                else if (decl is Predicate)
                    lst.Add(((Predicate)decl).Name);
            }

            return lst;
        }


        public Program getProgram(string filename)
        {
            Program dafnyProgram = null;
            List<DafnyFile> dafnyFiles;
            List<string> otherFiles;
            string[] args = new string[] { filename };
            DafnyDriver.ProcessCommandLineArguments(args, out dafnyFiles, out otherFiles);
            ErrorReporter reporter = new ConsoleErrorReporter();
            string s=Microsoft.Dafny.Main.ParseCheck(dafnyFiles, "", reporter, out dafnyProgram);
           
            return dafnyProgram;
        }

        public Method FindMethod(string filename, int line)
        {

            var dafnyProgram = getProgram(filename);
            List<TopLevelDecl> decls = new List<TopLevelDecl>();
            foreach (var module in dafnyProgram.Modules())
            {
                decls.AddRange(module.TopLevelDecls);
            }
            var callables = ModuleDefinition.AllCallables(decls);
//            var method = callables.Where(c => c is Method
//                && ((((Method)c).tok.line <= offset
//                && ((Method)c).BodyEndTok.line >= offset) || ((Method)c).tok.line == offset));

            foreach(var m in callables)
            {
                if(m is Method && m.Tok.line<= line)
                {
                    var method = m as Method;
                    int lastTokLine = m.Tok.line;
                    if (method.Ens.Count > 0 )
                    {
                        lastTokLine = method.Ens.Last().E.tok.line;
                    }
                    else if (method.Req.Count > 0)
                    {
                        lastTokLine = method.Req.Last().E.tok.line;
                    }
                    if (lastTokLine >= line)
                        return method;
                }
            }
            //           if (method.Any())
            //              return method.First() as Method;
            //          else return null;
            return null;
        }
       
    }
}

