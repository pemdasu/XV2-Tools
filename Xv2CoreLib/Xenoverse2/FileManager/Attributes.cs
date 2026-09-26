using System;
using Xv2CoreLib.Resource;

namespace Xv2CoreLib
{
    /// <summary>
    /// Marks this method as a file type load method that <see cref="FileManager"/>
    /// will attempt to use. The method must have one of two possible signatures:
    /// both must be static and return the declaring type. Only a single <see cref="FileLoadAttribute"/> can exist in a type.
    /// </summary>
    /// <remarks>
    /// <code>
    /// public static FILE_Type Load(byte[] bytes)
    /// 
    /// public static FILE_Type Load(string path, Xv2FileIO fileIo, bool onlyFromCpk)
    /// </code>
    /// 
    /// Note: The path in the second example is relative to the game data folder. You are expected to use either <see cref="Xv2FileIO.GetFileFromGame(string, bool, bool)"/> or <see cref="FileManager.GetBytesFromGame(string, bool, bool)"/> to load the files
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class FileLoadAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets a value indicating whether FileManager should still invoke the load method even if the file it is attempting to load is missing. 
        /// </summary>
        /// <remarks>When set to true, the input byte array can be null (when using the basic LoadFile delegate signature).</remarks>
        public bool AllowMissingFile { get; set; }
    }

    /// <summary>
    /// Marks this method as a file type save method that <see cref="FileManager"/>
    /// will attempt to use. The method must have one of two possible signatures and
    /// both must be instance methods. Only a single <see cref="FileSaveAttribute"/> can exist in a type.
    /// </summary>
    /// <remarks>
    /// <code>
    /// public byte[] Write()
    /// 
    /// public void Write(string absolutePath)
    /// </code>
    /// 
    /// Note: The first signature writes the file to a byte array and returns it.
    /// The second signature is for saving to disk and the path is an absolute path at which the file is to be saved.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class FileSaveAttribute : Attribute
    {
    }
}
