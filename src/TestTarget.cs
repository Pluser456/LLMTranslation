// 仅供自动化测试用的目标窗口: 一个含多行文本框的小窗体
using System;
using System.Drawing;
using System.Windows.Forms;

namespace TestTarget
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Form f = new Form();
            f.Text = "TestTargetApp";
            f.Size = new Size(500, 300);
            f.StartPosition = FormStartPosition.CenterScreen;
            TextBox tb = new TextBox();
            tb.Multiline = true;
            tb.Dock = DockStyle.Fill;
            tb.BackColor = Color.LightGreen;   // 绿色背景, 便于自动化像素判定胶囊
            tb.Font = new Font("Consolas", 11f);
            tb.Text = "In this section we discuss the spectral properties of the operator. " +
                "Let $T$ be a bounded linear operator on a Hilbert space $H$, and suppose that the resolvent " +
                "$(T - \\lambda I)^{-1}$ is compact for some $\\lambda$ in the resolvent set. " +
                "Then the spectrum consists of eigenvalues of finite multiplicity with no accumulation point except possibly at infinity. " +
                "Moreover, the eigenvectors form a Riesz basis of $H$, and for every $x \\in H$ we have the expansion " +
                "$$x = \\sum_{n=1}^{\\infty} \\langle x, v_n \\rangle u_n,$$ " +
                "where $u_n$ and $v_n$ denote the eigenvectors of $T$ and its adjoint respectively. " +
                "The convergence of this series is unconditional, and the partial sums satisfy the uniform bound " +
                "$\\| \\sum_{n \\le N} \\langle x, v_n \\rangle u_n \\| \\le C \\| x \\|$ with a constant $C$ independent of $N$. " +
                "These classical facts provide the foundation for the perturbation analysis carried out below, " +
                "where we estimate the deviation of the perturbed eigenvalues in terms of the norm of the perturbation, " +
                "and we show that the Riesz basis property is preserved under sufficiently small perturbations of the operator.";
            f.Controls.Add(tb);
            Application.Run(f);
        }
    }
}
