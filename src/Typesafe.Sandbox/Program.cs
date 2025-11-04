using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Typesafe.With;

namespace Typesafe.Sandbox
{
    interface IPerson
    {
        string Name { get; set; }
    }

    interface IStudent : IPerson
    {
        House House { get; set; }
    }

    class HogwartsStudents : IStudent
    {
        public string Name { get; set; }
        public House House { get; set; }

        public override string ToString() => $"Name={Name};House={House}";
    }
    
    class Person
    {
        public string Name { get; }
        public int Age { get; }
        public string LastName { get; set; }

        public Person(string name, int age)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Age = age;
        }

        public override string ToString() => $"Name={Name}; Age={Age}; LastName={LastName}; HashCode={GetHashCode()};";
    }

    class NoCtor
    {
        public string Name { get; set; }

        public override string ToString() => $"Name={Name};";
    }

    class UnrelatedType
    {
    }
    
    class Student
    {
        public string Name { get; }
        public House House { get; }
    
        public Student(string name, House house) => (Name, House) = (name, house);
    }

    enum House
    {
        Gryffindor,
        Slytherin
    }
    
    public class Parent
    {
        public Child Child { get; set; }
    }

    public class Child
    {
        public string Name { get; set; }
    }
    
    class Program
    {
        static T Nest<T, TValue>(T child1, TValue value, Expression<Func<T, TValue>> picker) where T : class
        {
            var withMethod = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Skip(1)
                .FirstOrDefault(m => m.Name == "With" && m.GetParameters().Length == 3)
                ?.MakeGenericMethod(typeof(T), typeof(TValue));

            var c1 = Expression.Parameter(typeof(T), "c1");
            var c = Expression.Parameter(typeof(T), "c");
            var withCall = Expression.Call(
                withMethod,
                c,
                Expression.Lambda(
                    Expression.MakeMemberAccess(
                        c1,
                        (picker.Body as MemberExpression).Member
                    ),
                    c1
                ),
                Expression.Constant(value, typeof(TValue))
            );
            var lambdaExpression = Expression.Lambda(withCall, c);
            var @delegate = lambdaExpression.Compile();
            var new1 = @delegate.DynamicInvoke(child1);

            return new1 as T;
        }
        
        static Expression<Func<T, T>> Expr<T, TValue>(Expression<Func<T, TValue>> picker, TValue value)
        {
            var withMethod = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Skip(1)
                .FirstOrDefault(m => m.Name == "With" && m.GetParameters().Length == 3)
                ?.MakeGenericMethod(typeof(T), typeof(TValue));

            var c1 = Expression.Parameter(typeof(T), "c1");
            var c = Expression.Parameter(typeof(T), "c");
            var withCall = Expression.Call(
                withMethod,
                c,
                Expression.Lambda(
                    Expression.MakeMemberAccess(
                        c1,
                        (picker.Body as MemberExpression).Member
                    ),
                    c1
                ),
                Expression.Constant(value, typeof(TValue))
            );

            return Expression.Lambda<Func<T, T>>(withCall, c);
        }
        
        static Expression<Func<T, T>> Expr<T, TValue>(Expression<Func<T, TValue>> picker, Expression<Func<TValue, TValue>> value)
        {
            var withMethod = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "With" && m.GetParameters().Length == 3)
                ?.MakeGenericMethod(typeof(T), typeof(TValue));

            var c1 = Expression.Parameter(typeof(T), "c1");
            var c = Expression.Parameter(typeof(T), "c");
            var withCall = Expression.Call(
                withMethod,
                c,
                Expression.Lambda(
                    Expression.MakeMemberAccess(
                        c1,
                        (picker.Body as MemberExpression).Member
                    ),
                    c1
                ),
                value
            );

            return Expression.Lambda<Func<T, T>>(withCall, c);
        }
        
        static Expression<Func<T, T>> NestExpr<T, TValue>(Expression<Func<T, TValue>> picker, TValue value)
        {
            var withMethod = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Skip(1)
                .FirstOrDefault(m => m.Name == "With" && m.GetParameters().Length == 3)
                ?.MakeGenericMethod(typeof(T), typeof(TValue));

            var c1 = Expression.Parameter(typeof(T), "c1");
            var c = Expression.Parameter(typeof(T), "c");
            var withCall = Expression.Call(
                withMethod,
                c,
                Expression.Lambda(
                    Expression.MakeMemberAccess(
                        c1,
                        (picker.Body as MemberExpression).Member
                    ),
                    c1
                ),
                Expression.Constant(value, typeof(TValue))
            );

            return Expression.Lambda<Func<T, T>>(withCall, c);
        }
        
        static Expression<Func<T, T>> NestExpr<T, TValue>(Expression<Func<T, TValue>> picker, Expression<Func<TValue, TValue>> value)
        {
            var withMethod = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "With" && m.GetParameters().Length == 3)
                ?.MakeGenericMethod(typeof(T), typeof(TValue));

            var c1 = Expression.Parameter(typeof(T), "c1");
            var c = Expression.Parameter(typeof(T), "c");
            var withCall = Expression.Call(
                withMethod,
                c,
                Expression.Lambda(
                    Expression.MakeMemberAccess(
                        c1,
                        (picker.Body as MemberExpression).Member
                    ),
                    c1
                ),
                value
            );

            return Expression.Lambda<Func<T, T>>(withCall, c);
        }

        static Expression<Func<T, T>> NestedWithQueue<T, TValue>(T instance, Expression<Func<T, TValue>> picker, Expression<Func<TValue, TValue>> value)
        {
            // Unwrap the property chain: x => x.y.z => [x, y, z]
            var members = new Queue<MemberExpression>();
            var expr = picker.Body;
            
            while (expr is MemberExpression memberExpr)
            {
                members.Enqueue(memberExpr);
                expr = memberExpr.Expression;
            }

            var current = members.Dequeue();
            var parent = members.Peek();
            
            var childParam = Expression.Parameter(current.Expression.Type, "y");
            var childLambda = Expression.Lambda(
                Expression.MakeMemberAccess(childParam, current.Member),
                childParam
            );
            
            var exprMethod = typeof(Program).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == "Expr")
                .Skip(1)
                .FirstOrDefault();

            var makeGenericMethod = exprMethod.MakeGenericMethod(current.Expression.Type, typeof(TValue));

            var methodCallExpression = Expression.Call(
                makeGenericMethod,
                childLambda,
                value
            );
            var lambdaExpression = Expression.Lambda(methodCallExpression, childParam);

            var lambda = CreateLambda(current, value, 1);

            var lambda1 = CreateLambda(parent, lambda, 2);

            // Do it for the parent
            var parentExprMethod = exprMethod.MakeGenericMethod(parent.Expression.Type, current.Expression.Type);
            var parentExprCallExpression = Expression.Call(
                parentExprMethod,
                childLambda,
                value
            );
            var dynamicInvoke = lambdaExpression.Compile().DynamicInvoke(parent);
            
            var expression = makeGenericMethod.Invoke(null, new object[] { childLambda, value });
            var expression1 = expression as Expression<Func<T, T>>;

            var parentParam = Expression.Parameter(parent.Expression.Type, "z");
            var parentLambda = Expression.Lambda(
                Expression.MakeMemberAccess(parentParam, parent.Member),
                parentParam
            );
            
            return null;

            LambdaExpression CreateLambda(MemberExpression thisMember, LambdaExpression thatValue, int i)
            {
                var thisParam = Expression.Parameter(thisMember.Expression.Type, $"y{i}");
                var thisLambda = Expression.Lambda(
                    Expression.MakeMemberAccess(thisParam, thisMember.Member),
                    thisParam
                );
            
                var t = false;

                if (t)
                {
                    var x = thatValue.Compile().DynamicInvoke((instance as Parent).Child);
                    thatValue = x as LambdaExpression;
                    // Expression.Invoke(thatValue)
                }
                
                var exprMethod1 = typeof(Program).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.Name == "Expr")
                    .Skip(1)
                    .FirstOrDefault()
                    ?.MakeGenericMethod(thisMember.Expression.Type, thatValue.ReturnType);

                
                var methodCallExpression1 = Expression.Call(
                    exprMethod1,
                    thisLambda,
                    thatValue
                );
                var lambdaExpression1 = Expression.Lambda(methodCallExpression1, thisParam);

                return lambdaExpression1;
            }
        }
        
        static Expression<Func<T, T>> NestedWithQueue1<T, TValue>(Expression<Func<T, TValue>> picker, Expression<Func<TValue, TValue>> value)
        {
            // Unwrap the property chain: x => x.y.z => [x, y, z]
            var members = new Queue<MemberExpression>();
            var expr = picker.Body;
            
            while (expr is MemberExpression memberExpr)
            {
                members.Enqueue(memberExpr);
                expr = memberExpr.Expression;
            }

            var withMethod = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "With" && m.GetParameters().Length == 3);

            var root = members.Dequeue();

            var rootExpression = BuildLambda(root, 0, value);
            
            for (var i = 1; i < 1 + members.Count; i++)
            {
                var current1 = members.Dequeue();

                var lambdaExpression = BuildLambda(current1, i, rootExpression);
                rootExpression = lambdaExpression;
            }

            return rootExpression as Expression<Func<T, T>>;

            LambdaExpression BuildLambda(MemberExpression current, int i, Expression value1)
            {
                var thisParam = Expression.Parameter(current.Expression.Type, $"y{i}");
                var thisLambda = Expression.Lambda(
                    Expression.MakeMemberAccess(thisParam, current.Member),
                    thisParam
                );

                var genericWithMethod = withMethod.MakeGenericMethod(thisParam.Type, current.Type);
                
                var c1 = Expression.Parameter(thisParam.Type, $"c1_{i}");
                var c = Expression.Parameter(thisParam.Type, $"c_{i}");
                var withCall = Expression.Call(
                    genericWithMethod,
                    c,
                    Expression.Lambda(
                        Expression.MakeMemberAccess(
                            c1,
                            current.Member
                        ),
                        c1
                    ),
                    value1
                );

                return Expression.Lambda(withCall, c);
            }
        }
        
        static Expression<Func<T, T>> NestedWithStack<T, TValue>(T instance, Expression<Func<T, TValue>> picker, Expression<Func<TValue, TValue>> value)
        {
            // Unwrap the property chain: x => x.y.z => [x, y, z]
            var members = new Stack<MemberExpression>();
            var expr = picker.Body;
            
            while (expr is MemberExpression memberExpr)
            {
                members.Push(memberExpr);
                expr = memberExpr.Expression;
            }

            var withMethod = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "With" && m.GetParameters().Length == 3);

            for (var i = 0; i < members.Count; i++)
            {
                var current1 = members.Pop();

                var lambdaExpression = BuildLambda(current1, i);
            }

            return null;

            LambdaExpression BuildLambda(MemberExpression current, int i)
            {
                var thisParam = Expression.Parameter(current.Expression.Type, $"y{i}");
                var thisLambda = Expression.Lambda(
                    Expression.MakeMemberAccess(thisParam, current.Member),
                    thisParam
                );

                var genericWithMethod = withMethod.MakeGenericMethod(thisParam.Type, current.Type);
                
                var c1 = Expression.Parameter(thisParam.Type, $"c1_{i}");
                var c = Expression.Parameter(thisParam.Type, $"c_{i}");
                var withCall = Expression.Call(
                    genericWithMethod,
                    c,
                    Expression.Lambda(
                        Expression.MakeMemberAccess(
                            c1,
                            current.Member
                        ),
                        c1
                    ),
                    thisLambda
                );

                return Expression.Lambda<Func<T, T>>(withCall, c);
            }
            
            LambdaExpression CreateLambda(MemberExpression thisMember, LambdaExpression thatValue, int i)
            {
                var thisParam = Expression.Parameter(thisMember.Expression.Type, $"y{i}");
                var thisLambda = Expression.Lambda(
                    Expression.MakeMemberAccess(thisParam, thisMember.Member),
                    thisParam
                );
            
                var t = false;

                if (t)
                {
                    var x = thatValue.Compile().DynamicInvoke((instance as Parent).Child);
                    thatValue = x as LambdaExpression;
                    // Expression.Invoke(thatValue)
                }
                
                var exprMethod1 = typeof(Program).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.Name == "Expr")
                    .Skip(1)
                    .FirstOrDefault()
                    ?.MakeGenericMethod(thisMember.Expression.Type, thatValue.ReturnType);

                
                var methodCallExpression1 = Expression.Call(
                    exprMethod1,
                    thisLambda,
                    thatValue
                );
                var lambdaExpression1 = Expression.Lambda(methodCallExpression1, thisParam);

                return lambdaExpression1;
            }
        }
        
        static void Main(string[] args)
        {
            {
                var propertyType = typeof(int);
                var instanceType = typeof(string);
                var funcType = typeof(Func<,>);
                var propertyPickerType = typeof(Expression<>).MakeGenericType(funcType.MakeGenericType(instanceType, propertyType));
                var propertyValueFactoryType = typeof(Expression<>).MakeGenericType(funcType.MakeGenericType(propertyType, propertyType));
                var method = typeof(ObjectExtensions)
                    .GetMethod(
                        nameof(ObjectExtensions.With),
                        //BindingFlags.Public | BindingFlags.Static,
                        new[] { instanceType, propertyPickerType, propertyValueFactoryType }
                    );
                var methodInfo = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == nameof(ObjectExtensions.With))
                    .Where(m => m.GetParameters()[1] .ParameterType.GetGenericTypeDefinition() == typeof(Expression<>))
                    .Where(m => m.GetParameters()[2] .ParameterType.GetGenericTypeDefinition() == typeof(Expression<>))
                    .FirstOrDefault();
                Console.WriteLine(methodInfo);
            }
            
            {
                // Nested update
                var child1 = new Child { Name = "Harry" };
                var parent = new Parent { Child = child1 };

                {
                    MethodInfo firstOrDefault = typeof(ObjectExtensions)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "With" && m.GetParameters().Length == 3);
                    var methodInfo = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => m.Name == nameof(ObjectExtensions.With))
                        .Where(m => m.GetParameters()[1] .ParameterType.GetGenericTypeDefinition() == typeof(Expression<>))
                        .FirstOrDefault();

                    // typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                        // .Where(m => m.Name == nameof(ObjectExtensions.With))
                        // .Where(m => m.GetParameters()[0].ParameterType == typeof(Parent))
                }
                
                {
                    var nestedWith = NestedWithQueue1<Parent, string>(p => p.Child.Name, s => "Ron");
                    Console.WriteLine("NestedWithQueue1: " + nestedWith.Compile().Invoke(parent).Child.Name);
                }

                
                // {
                //     var nestedWith = NestedWithStack(parent, p => p.Child.Name, s => "Ron");
                //     Console.WriteLine(nestedWith.Compile().Invoke(parent).Child.Name);
                // }
                
                // {
                //     var nestedWith = NestedWithQueue(parent, p => p.Child.Name, s => "Ron");
                //     Console.WriteLine(nestedWith.Compile().Invoke(parent).Child.Name);
                // }
                
                {
                    var withMethod = typeof(ObjectExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Skip(1)
                        .FirstOrDefault(m => m.Name == "With" && m.GetParameters().Length == 3)
                        ?.MakeGenericMethod(typeof(Child), typeof(string));

                    var c1 = Expression.Parameter(typeof(Child), "c1");
                    var c = Expression.Parameter(typeof(Child), "c");
                    var withCall = Expression.Call(
                        withMethod,
                        c,
                        Expression.Lambda(
                            Expression.MakeMemberAccess(
                                c1,
                                typeof(Child).GetProperty(nameof(Child.Name))
                            ),
                            c1
                        ),
                        Expression.Constant("Draco", typeof(string))
                    );
                    var lambdaExpression = Expression.Lambda(withCall, c);
                    var @delegate = lambdaExpression.Compile();
                    var new1 = @delegate.DynamicInvoke(child1);
                    Console.WriteLine("Manual: " + (new1 as Child).Name);
                    Console.WriteLine("Nest (concrete value): " + Nest(child1, "Draco", ch => ch.Name).Name);
                }

                var nestExpr = NestExpr<Child, string>(ch => ch.Name, "Draco");
                Console.WriteLine("NestExpr (concrete value): " + nestExpr.Compile().Invoke(child1).Name);
                
                var childNestExpr = NestExpr<Child, string>(ch => ch.Name, s => "Draco");
                Console.WriteLine("NestExpr (factory): " + childNestExpr.Compile().Invoke(child1).Name);

                var childExpr = Expr<Child, string>(ch => ch.Name, "Draco");
                Console.WriteLine("Expr (concrete value): " + childExpr.Compile().Invoke(child1).Name);
                
                var childExprFactory = Expr<Child, string>(ch => ch.Name, s => "Draco");
                Console.WriteLine("Expr (concrete value): " + childExprFactory.Compile().Invoke(child1).Name);
                
                var child2 = new Child { Name = "Hermione" };
                Console.WriteLine("Nest (concrete value): " + Nest<Parent, Child>(parent, child2, p => p.Child).Child.Name);
                
                var with = parent.With(p => p.Child, new Child { Name = "Malfoy" });
                Console.WriteLine("With (concrete value): " + with.Child.Name);
                
                var parentNestExpr = NestExpr<Parent, Child>(p => p.Child, child2);
                Console.WriteLine("NestExpr (concrete value): " + parentNestExpr.Compile().Invoke(parent).Child.Name);
                
                var parentNestedNestExpr = NestExpr<Parent, Child>(p => p.Child, ch => child2);
                Console.WriteLine("NestExpr (factory): " + parentNestedNestExpr.Compile().Invoke(parent).Child.Name);
                
                var theExpr = NestExpr<Parent, Child>(p => p.Child, childExprFactory);
                Console.WriteLine("NestExpr (expression): " + theExpr.Compile().Invoke(parent).Child.Name);
                
                var theExpr1 = NestExpr<Parent, Child>(p => p.Child, Expr<Child, string>(chi => chi.Name, "Ron"));
                Console.WriteLine("NestExpr (expression): " + theExpr1.Compile().Invoke(parent).Child.Name);
                
                var theExpr2 = Expr<Parent, Child>(p => p.Child, Expr<Child, string>(chi => chi.Name, "Ron"));
                Console.WriteLine("Expr (expression): " + theExpr2.Compile().Invoke(parent).Child.Name);
                
                var child = parent.NestedWith(p => p.Child.Name,"Draco");
                Console.WriteLine("NestedWith (before): " + parent.Child.Name);
                Console.WriteLine("NestedWith (after): " + child.Child.Name);
            }
            {
                IStudent harry = new HogwartsStudents { Name = "Harry", House = House.Gryffindor }; 
                IStudent draco = harry.With(p => p.Name, "Draco");
                Console.WriteLine(draco); // Prints "Draco"
            }
            {
                var harry = new Student("Harry Potter", House.Gryffindor);
                var malfoy = harry
                    .With(p => p.Name, "Malfoy")
                    .With(p => p.House, house => house == House.Slytherin ? House.Gryffindor : house);

                Console.WriteLine(malfoy.Name); // Prints "Malfoy"
                Console.WriteLine(malfoy.House); // Prints "Gryffindor"
            }
            
            {
                var harry = new Student("Harry Potter", House.Gryffindor);
                var malfoy = harry
                    .With(p => p.Name, name => name.Length == 1 ? name : "Snape")
                    .With(p => p.House, House.Slytherin);
                Console.WriteLine(malfoy.Name);
            }
            
            var person = new Person("Søren", 10);
            Console.WriteLine(person);
            
            var lasse = person
                .With(p => p.Name, "Lasse");
            Console.WriteLine(lasse);
            
            var youngerSoren = person.With(p => p.Age, 5);
            Console.WriteLine(youngerSoren);
            
            var withLastName = person
                .With(p => p.Name, "Test")
                .With(p => p.LastName, "Guldmund")
                .With(p => p.Age, 5);
            Console.WriteLine(withLastName);
            
            var sorenAgain = person
                .With(p => p.Name, "Søren");
            Console.WriteLine(sorenAgain);
            
            var noCtor = new NoCtor {Name = "Søren"};
            Console.WriteLine(noCtor);
            
            var noCtor1 = noCtor.With(p => p.Name, "Test");
            Console.WriteLine(noCtor1);
        }
    }
}